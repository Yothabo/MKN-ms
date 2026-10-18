using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.AttendanceRuleEngine;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D13_AttendanceRule;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D8_AttendanceRecord;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 13.0 Attendance Rule Engine.
/// </summary>
/// <remarks>
/// Runs against mkn_test. Covers the acceptance-matrix rows for
/// 13.0: absence window boundaries, register scope, evaluation
/// order, outcome vocabulary, manual invocation, and required
/// settings.
/// </remarks>
public class AttendanceRuleEngineTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private MknDbContext _db = null!;
    private FakeDispatchNotificationService _notification = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _notification = new FakeDispatchNotificationService();
        _clock = new FakeTimeProvider(Now);

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
        int BranchId,
        int RoleId,
        int DefaultStatusId,
        int OtherStatusId,
        int MemberId,
        int AdminId);

    private async Task<Fixture> SeedAsync(bool registerEnabled = true)
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var role = new Role { Name = "Member", IsDeleted = false };
        var branch = new Branch
        {
            Name = "Main",
            Location = "Main Street",
            IsDeleted = false,
            UsesAttendanceRegister = null,
        };
        var defaultStatus = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var otherStatus = new MemberStatus
        {
            Name = "Inactive", IsRosterable = false, IsDefault = false, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(defaultStatus);
        _db.MemberStatuses.Add(otherStatus);
        _db.PermissionTiers.Add(tier);

        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = true,
        });
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "AttendanceRegisterEnabled",
            Value = registerEnabled ? "true" : "false",
            Required = true,
        });

        await _db.SaveChangesAsync();

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId,
            RoleId = role.RoleId,
            MemberStatusId = defaultStatus.MemberStatusId,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return new Fixture(
            branch.BranchId,
            role.RoleId,
            defaultStatus.MemberStatusId,
            otherStatus.MemberStatusId,
            member.MemberId,
            admin.AdminId);
    }

    private async Task<int> SeedOccurrenceWithAttendanceAsync(
        Fixture f,
        DateOnly date)
    {
        // A schedule and occurrence, so an AttendanceRecord can point
        // at a real occurrence.
        var slot = await _db.BranchTimeSlots
            .FirstOrDefaultAsync(bts => bts.BranchId == f.BranchId);

        if (slot is null)
        {
            var timeOfDayId = await _db.TimeOfDays
                .Select(t => t.TimeOfDayId).FirstAsync();
            slot = new BranchTimeSlot
            {
                BranchId = f.BranchId,
                DayOfWeek = "Sunday",
                TimeOfDayId = timeOfDayId,
            };
            _db.BranchTimeSlots.Add(slot);
            await _db.SaveChangesAsync();
        }

        var serviceTypeId = await _db.ServiceTypes
            .Select(st => st.ServiceTypeId).FirstAsync();

        var definition = new ServiceDefinition
        {
            Name = "Svc",
            ServiceTypeId = serviceTypeId,
            OwningBranchId = null,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

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
            Date = date,
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        return occurrence.OccurrenceId;
    }

    private async Task AddAttendanceAsync(int memberId, int occurrenceId)
    {
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = occurrenceId,
            Timestamp = Now,
            Source = "Manual",
        });
        await _db.SaveChangesAsync();
    }

    private async Task<int> AddRuleAsync(
        string name,
        string triggerType,
        string? triggerValue,
        string outcomeType,
        int? outcomeStatusId = null,
        bool enabled = true)
    {
        var rule = new AttendanceRule
        {
            Name = name,
            TriggerType = triggerType,
            TriggerValue = triggerValue,
            OutcomeType = outcomeType,
            OutcomeStatusId = outcomeStatusId,
            Enabled = enabled,
            IsDeleted = false,
        };
        _db.AttendanceRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule.AttendanceRuleId;
    }

    // -----------------------------------------------------------------
    // AbsenceDays
    // -----------------------------------------------------------------

    [Fact]
    public async Task AbsenceDays_91Days_NoAttendance_Fires()
    {
        var f = await SeedAsync();
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(1);
        result.StatusChangesApplied.Should().Be(1);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    [Fact]
    public async Task AbsenceDays_ExactBoundary_AttendanceOnBoundaryDay_DoesNotFire()
    {
        var f = await SeedAsync();
        // Boundary: today - 90. An attendance record on that date
        // satisfies the window because the window is inclusive.
        var boundaryDate = DateOnly.FromDateTime(Now.Date).AddDays(-90);
        var occId = await SeedOccurrenceWithAttendanceAsync(f, boundaryDate);
        await AddAttendanceAsync(f.MemberId, occId);

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task AbsenceDays_AttendanceOutsideWindow_Fires()
    {
        var f = await SeedAsync();
        // Attendance 91 days ago is outside the [today-90, today]
        // window.
        var outsideDate = DateOnly.FromDateTime(Now.Date).AddDays(-91);
        var occId = await SeedOccurrenceWithAttendanceAsync(f, outsideDate);
        await AddAttendanceAsync(f.MemberId, occId);

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // Register scope
    // -----------------------------------------------------------------

    [Fact]
    public async Task AbsenceDays_RegisterOffAtBranch_IsSkipped()
    {
        var f = await SeedAsync(registerEnabled: false);
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task BranchOverride_OnWithGlobalOff_Applies()
    {
        var f = await SeedAsync(registerEnabled: false);

        var branch = await _db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = true;
        await _db.SaveChangesAsync();

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // Evaluation order (B8)
    // -----------------------------------------------------------------

    [Fact]
    public async Task TwoSetStatusRules_LaterWins()
    {
        var f = await SeedAsync();
        await AddRuleAsync("first", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);
        await AddRuleAsync("second", "AbsenceDays", "90", "SetStatus", f.DefaultStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(2);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task SetStatusAndNotify_BothTakeEffect()
    {
        var f = await SeedAsync();
        await AddRuleAsync("status", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);
        await AddRuleAsync("notify", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
        result.NotificationsDispatched.Should().Be(1);
    }

    [Fact]
    public async Task TwoNotifyRules_TwoDispatches()
    {
        var f = await SeedAsync();
        await AddRuleAsync("notify-1", "AbsenceDays", "90", "Notify");
        await AddRuleAsync("notify-2", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.NotificationsDispatched.Should().Be(2);
    }

    [Fact]
    public async Task SetStatus_OnMemberAlreadyInTargetStatus_StateUnchanged()
    {
        // The member is already in the target status. The rule fires
        // and the engine applies the write; the resulting state is
        // unchanged. The counter records the write, not the
        // difference. §13.6.
        var f = await SeedAsync();
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.DefaultStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    // -----------------------------------------------------------------
    // Idempotency (B10)
    // -----------------------------------------------------------------

    [Fact]
    public async Task RepeatedRun_TriggerStillHolds_OutcomeReapplied()
    {
        var f = await SeedAsync();
        await AddRuleAsync("notify", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var first = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });
        var second = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        first.NotificationsDispatched.Should().Be(1);
        second.NotificationsDispatched.Should().Be(1);
    }

    // -----------------------------------------------------------------
    // Manual rules (B11)
    // -----------------------------------------------------------------

    [Fact]
    public async Task ManualRule_NotEvaluatedByScheduledRun()
    {
        var f = await SeedAsync();
        await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(0);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_FiresUnconditionally()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", "999", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(1);
        result.StatusChangesApplied.Should().Be(1);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    [Fact]
    public async Task ManualInvocation_NonManualRule_Rejected()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Attendance Rule");
    }

    [Fact]
    public async Task ManualInvocation_NonexistentAdmin_Fails()
    {
        // §13.6: the manual invocation carries the ActingAdminID of the
        // administrator performing it, which resolves to an Admin row.
        // A positive id that names no row does not satisfy the
        // requirement.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = 9999,
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("not_found");
        result.ErrorDetail.Should().Contain("Admin 9999");

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_InactiveAdmin_Fails()
    {
        // §13.6: the ActingAdminID resolves to an Admin row whose
        // IsActive = true. An inactive admin does not satisfy the
        // requirement.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var admin = await _db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("not_found");
        result.ErrorDetail.Should().Contain("is not active");

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_TwiceSameDay_OutcomeAppliedTwice()
    {
        // §13.6: repeat invocation applies the outcome again; the
        // engine does not maintain per-rule execution history.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);

        var first = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });
        var second = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        first.Status.Should().Be("Success");
        first.StatusChangesApplied.Should().Be(1);
        second.Status.Should().Be("Success");
        second.StatusChangesApplied.Should().Be(1);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // ReadmissionCount
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReadmissionCount_AtThreshold_Fires()
    {
        var f = await SeedAsync();

        _db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        _db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 6, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await _db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "2", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
    }

    [Fact]
    public async Task ReadmissionCount_BelowThreshold_DoesNotFire()
    {
        var f = await SeedAsync();

        _db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await _db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "2", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);
    }

    [Fact]
    public async Task ReadmissionCount_RegisterOff_StillApplies()
    {
        var f = await SeedAsync(registerEnabled: false);

        _db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await _db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "1", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
    }

    [Fact]
    public async Task Engine_DoesNotWriteReadmission()
    {
        // §13.6: the engine reads Readmission and never writes it.
        // 3.0 owns Readmission creation.
        var f = await SeedAsync();

        _db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await _db.SaveChangesAsync();

        var before = await _db.Readmissions.CountAsync();
        before.Should().Be(1);

        await AddRuleAsync("readmit", "ReadmissionCount", "1", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);

        var after = await _db.Readmissions.CountAsync();
        after.Should().Be(before);
    }

    // -----------------------------------------------------------------
    // Required settings
    // -----------------------------------------------------------------

    [Fact]
    public async Task MissingAttendanceRegisterEnabled_Fails()
    {
        var f = await SeedAsync();

        var setting = await _db.SystemSettings
            .FirstAsync(s => s.Key == "AttendanceRegisterEnabled");
        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_missing");
    }

    [Fact]
    public async Task MissingApplicationTimeZone_Fails()
    {
        var f = await SeedAsync();

        var setting = await _db.SystemSettings
            .FirstAsync(s => s.Key == "ApplicationTimeZone");
        _db.SystemSettings.Remove(setting);
        await _db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_missing");
    }

    // -----------------------------------------------------------------
    // Disabled rule
    // -----------------------------------------------------------------

    [Fact]
    public async Task DisabledRule_NotEvaluated()
    {
        var f = await SeedAsync();
        await AddRuleAsync("disabled", "AbsenceDays", "90", "SetStatus",
            f.OtherStatusId, enabled: false);

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(0);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }
    [Fact]
    public async Task SetStatus_OutcomeStatusSoftDeleted_RuleSkipped()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync(
            "status", "Manual", null, "SetStatus", f.OtherStatusId);

        // Soft-delete the outcome status. The rule was valid at write
        // time; the status is now soft-deleted. The engine must skip
        // the outcome rather than write a soft-deleted status.
        var status = await _db.MemberStatuses
            .FirstAsync(s => s.MemberStatusId == f.OtherStatusId);
        status.IsDeleted = true;
        status.IsActive = false;
        await _db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(_db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);

        var member = await _db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }
}
