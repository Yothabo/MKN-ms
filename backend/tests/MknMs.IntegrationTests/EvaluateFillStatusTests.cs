using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.EvaluateFillStatus;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 10.0 Evaluate Fill Status.
/// </summary>
public class EvaluateFillStatusTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, attendance_record, roster_assignment, " +
            "service_occurrence_duty, service_occurrence, service_schedule, " +
            "service_definition_duty, branch_time_slot, service_definition, " +
            "service_type, time_of_day, branch, identifier_history, eligibility, " +
            "duty_rule, admin, member, member_attribute_value, member_status, " +
            "attendance_rule_scope, attendance_rule, readmission, role, duty, " +
            "system_setting, outcome_state, assignment_status, permission_tier, " +
            "attribute_type, capability, event_branch, configuration_audit_log, " +
            "entity_deletion_policy, notification_subscription " +
            "RESTART IDENTITY CASCADE;");

        await TestSeed.EnsureRequiredSettingsAsync(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int OccurrenceId,
        int DutyId,
        int UnfilledStateId,
        int PartiallyFilledStateId,
        int FilledStateId,
        int CancelledStateId,
        int NonTerminalStatusId,
        int TerminalStatusId,
        int MemberOneId,
        int MemberTwoId);

    private async Task<Fixture> SeedFixtureAsync(
        bool includeRequiredSettings = true,
        bool includeCancelledSetting = false,
        bool includeRequiredDuty = true)
    {
        var unfilled = new OutcomeState { Name = "Unfilled" };
        var partially = new OutcomeState { Name = "Partially Filled" };
        var filled = new OutcomeState { Name = "Filled" };
        var cancelled = new OutcomeState { Name = "Cancelled" };
        _db.OutcomeStates.AddRange(unfilled, partially, filled, cancelled);

        var serviceType = new ServiceType { Name = "Regular" };
        _db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        _db.TimeOfDays.Add(timeOfDay);

        var role = new Role { Name = "Coordinator", IsActive = true };
        _db.Roles.Add(role);

        var duty = new Duty { Name = "Reading duty", IsActive = true };
        _db.Duties.Add(duty);

        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        _db.Branches.Add(branch);

        var proposed = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        _db.AssignmentStatuses.Add(proposed);
        _db.AssignmentStatuses.Add(declined);

        await _db.SaveChangesAsync();

        if (includeRequiredSettings)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateUnfilledID",
                Value = unfilled.OutcomeStateId.ToString(),
                Required = true,
            });
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStatePartiallyFilledID",
                Value = partially.OutcomeStateId.ToString(),
                Required = true,
            });
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateFilledID",
                Value = filled.OutcomeStateId.ToString(),
                Required = true,
            });
        }

        if (includeCancelledSetting)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateCancelledID",
                Value = cancelled.OutcomeStateId.ToString(),
                Required = false,
            });
        }

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(_db);

        var m1 = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        var m2 = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        _db.Members.Add(m1);
        _db.Members.Add(m2);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        // One duty, two required slots. This lets us exercise Unfilled
        // (0 active), Partially Filled (1 active), Filled (2 active).
        // The zero-duty case skips the row.
        if (includeRequiredDuty)
        {
            _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
            {
                ServiceDefId = definition.ServiceDefId,
                DutyId = duty.DutyId,
                RequiredSlotCount = 2,
            });
            await _db.SaveChangesAsync();
        }

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        return new Fixture(
            occurrence.OccurrenceId,
            duty.DutyId,
            unfilled.OutcomeStateId,
            partially.OutcomeStateId,
            filled.OutcomeStateId,
            cancelled.OutcomeStateId,
            proposed.AssignmentStatusId,
            declined.AssignmentStatusId,
            m1.MemberId,
            m2.MemberId);
    }

    private async Task AddAssignmentAsync(
        Fixture f,
        int memberId,
        int? statusId)
    {
        _db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = memberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            AssignmentStatusId = statusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    [Fact]
    public async Task Evaluate_WithZeroActiveAssignments_WritesUnfilled()
    {
        var f = await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);
        result.UnfilledCount.Should().Be(1);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithFewerActiveThanRequired_WritesPartiallyFilled()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.PartiallyFilledCount.Should().Be(1);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.PartiallyFilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithActiveMeetRequired_WritesFilled()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);
        await AddAssignmentAsync(f, f.MemberTwoId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.FilledCount.Should().Be(1);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.FilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithTerminalAssignments_DoesNotCountThem()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.TerminalStatusId);
        await AddAssignmentAsync(f, f.MemberTwoId, f.TerminalStatusId);

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.UnfilledCount.Should().Be(1);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithZeroRequiredDuties_ClassifiesAsFilled()
    {
        // Zero required duties. The effective required count is zero.
        // The zero-duty rule (§10.1.2, §12.1.2) classifies the
        // occurrence as Filled, because active (0) meets required (0).
        var f = await SeedFixtureAsync(includeRequiredDuty: false);
        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.FilledCount.Should().Be(1);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.FilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithCancelledCurrentStatus_Skips()
    {
        var f = await SeedFixtureAsync(includeCancelledSetting: true);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId = f.CancelledStateId;
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(0);
        result.SkippedCancelledCount.Should().Be(1);

        var after = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        after.FillStatusId.Should().Be(f.CancelledStateId);
    }

    [Fact]
    public async Task Evaluate_WithCancelledSettingAbsent_NoExclusion()
    {
        // No OutcomeStateCancelledID setting. The cancelled state is
        // not excluded; the occurrence is evaluated and the mechanical
        // classification overwrites its current FillStatusId.
        var f = await SeedFixtureAsync(includeCancelledSetting: false);

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId = f.CancelledStateId;
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);
        result.SkippedCancelledCount.Should().Be(0);
        result.UnfilledCount.Should().Be(1);

        var after = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        after.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    [Fact]
    public async Task Evaluate_WithoutRequiredSetting_Fails()
    {
        var f = await SeedFixtureAsync(includeRequiredSettings: false);
        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeState");

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutUnfilledSetting_Fails()
    {
        // Only OutcomeStateUnfilledID removed; the other two remain.
        // The whole run is refused, not just the affected occurrences.
        var f = await SeedFixtureAsync();

        var setting = await _db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStateUnfilledID");
        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStateUnfilledID");

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutPartiallyFilledSetting_Fails()
    {
        var f = await SeedFixtureAsync();

        var setting = await _db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStatePartiallyFilledID");
        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStatePartiallyFilledID");

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutFilledSetting_Fails()
    {
        var f = await SeedFixtureAsync();

        var setting = await _db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStateFilledID");
        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStateFilledID");

        var occ = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_IsIdempotent()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(_db);

        await service.RunAsync(new EvaluateFillStatusCommand { OccurrenceId = f.OccurrenceId });
        var first = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);

        await service.RunAsync(new EvaluateFillStatusCommand { OccurrenceId = f.OccurrenceId });
        var second = await _db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);

        second.FillStatusId.Should().Be(first.FillStatusId);
    }

    [Fact]
    public async Task Evaluate_SingleOccurrence_DoesNotTouchOtherOccurrences()
    {
        // A single-occurrence invocation must evaluate only that
        // occurrence. A second occurrence with the same required duty
        // is left untouched.
        var f = await SeedFixtureAsync();

        var scheduleId = await _db.ServiceOccurrences
            .Where(o => o.OccurrenceId == f.OccurrenceId)
            .Select(o => o.ScheduleId!.Value)
            .FirstAsync();

        var otherOccurrence = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 18),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(otherOccurrence);
        await _db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(_db);
        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);

        var untouched = await _db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == otherOccurrence.OccurrenceId);
        untouched.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithInvalidCommand_Fails()
    {
        await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand());

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }

    [Fact]
    public async Task Run_WithBothOccurrenceAndRange_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(_db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 31),
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }
}
