using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D12_MaterializerRun;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D9_EventProgram;
using MknMs.Persistence;
using Xunit;
using Npgsql;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 11.0 Materialize Occurrences.
/// </summary>
/// <remarks>
/// These tests run against a real PostgreSQL database named mkn_test.
/// They verify:
///   - Occurrences are created for each candidate date within the horizon.
///   - Occurrence fields are inherited correctly from the schedule and
///     service definition.
///   - GeneratedBy is System and CreatedBy is null.
///   - A MaterializerRun row is written for each invocation.
///   - Idempotency: a second run creates no additional rows.
///
/// Reference: docs/processes/operations/11.0-materialize-occurrences.md
/// and Specification §7, §8.
/// </remarks>
public class MaterializeOccurrencesTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // No migration seeds SystemSetting. 11.0 depends on
        // ApplicationTimeZone and the fixture seeds OccurrenceHorizonDays
        // per fact. Seed the required rows per fact. The helper is
        // idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    // Matrix row 11.0:8956bca71e61: One active weekly schedule, a 30-day horizon, and a fixed today of 2026-10-06 | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:8956bca71e61")]
    [Trait("MatrixRow", "11.0:5dd8ac473887")]
    public async Task Run_WithValidConfiguration_CreatesOccurrencesForAllCandidateDates()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(1);
        result.OccurrencesCreated.Should().Be(4);

        var occurrences = await Db.ServiceOccurrences.OrderBy(o => o.Date).ToListAsync();

        occurrences.Should().HaveCount(4);
        occurrences.Select(o => o.Date).Should().Equal(
            new DateOnly(2026, 10, 11),
            new DateOnly(2026, 10, 18),
            new DateOnly(2026, 10, 25),
            new DateOnly(2026, 11, 1));

        foreach (var occurrence in occurrences)
        {
            occurrence.ScheduleId.Should().NotBeNull();
            occurrence.EventId.Should().BeNull();
            occurrence.StartTime.Should().Be(new TimeOnly(9, 0));
            occurrence.GeneratedBy.Should().Be("System");
            occurrence.CreatedBy.Should().BeNull();
            occurrence.ChangedBy.Should().BeNull();
            occurrence.FillStatusId.Should().BeNull();
        }
    }

    // Matrix row 11.0:6e10beff1cdd: A prior run of 11.0 that created four occurrences | 11.0 runs again
    [Fact]
    [Trait("MatrixRow", "11.0:6e10beff1cdd")]
    [Trait("MatrixRow", "11.0:794e68708837")]
    public async Task Run_Twice_IsIdempotent()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var first = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        var countAfterFirst = await Db.ServiceOccurrences.CountAsync();

        var second = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        var countAfterSecond = await Db.ServiceOccurrences.CountAsync();

        first.OccurrencesCreated.Should().Be(4);
        second.OccurrencesCreated.Should().Be(0);
        countAfterSecond.Should().Be(countAfterFirst);

        var runs = await Db.MaterializerRuns.OrderBy(r => r.RunId).ToListAsync();
        runs.Should().HaveCount(2);
        runs.Should().AllSatisfy(r => r.Status.Should().Be("Success"));
    }

    // Matrix row 11.0:50321a8a0fb8: OccurrenceHorizonDays absent | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:50321a8a0fb8")]
    public async Task Run_WithoutRequiredSetting_RecordsFailure()
    {
        // Seed everything except the required setting.
        await SeedMinimalConfigurationAsync(includeHorizonSetting: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesCreated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OccurrenceHorizonDays");

        var occurrences = await Db.ServiceOccurrences.CountAsync();
        occurrences.Should().Be(0);

        var run = await Db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Failure");
        run.ErrorDetail.Should().Contain("OccurrenceHorizonDays");
    }

    // Matrix row 11.0:085329001058: A Manual trigger with TriggeredByAdminId = null | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:085329001058")]
    public async Task Run_WithManualTriggerAndNoAdmin_Throws()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var act = async () => await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Manual",
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TriggeredByAdminId*");
    }

    private async Task SeedMinimalConfigurationAsync(
        bool includeHorizonSetting = true,
        int horizonDays = 30,
        bool definitionActive = true,
        bool slotActive = true,
        string dayOfWeek = "Sunday")
    {
        if (includeHorizonSetting)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "OccurrenceHorizonDays",
                Value = horizonDays.ToString(),
                Required = true,
            });
        }

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var serviceType = new ServiceType { Name = "Regular" };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };

        Db.TimeOfDays.Add(timeOfDay);
        Db.ServiceTypes.Add(serviceType);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = dayOfWeek,
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsActive = slotActive,
        };

        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = definitionActive,
        };

        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };

        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();
    }

    private async Task<int> SeedAdminAsync()
    {
        var role = new Role { Name = "Coordinator", IsActive = true, IsDeleted = false };
        var branch = await Db.Branches.FirstAsync();
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier { Name = "Full Admin", IsDefault = true, IsDeleted = false };

        Db.Roles.Add(role);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        var member = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-900-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        return admin.AdminId;
    }

    // Matrix row 11.0:59a5667c79c2: A prior run and a concurrent run contending for the advisory lock | Both complete
    [Fact]
    [Trait("MatrixRow", "11.0:59a5667c79c2")]
    [Trait("MatrixRow", "11.0:d9e58793867c")]
    public async Task Run_WhenLockHeldExternally_FailsWithOverlapMessage()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        await using var externalConnection = new NpgsqlConnection(ConnectionString);
        await externalConnection.OpenAsync();

        await using (var lockCmd = externalConnection.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_try_advisory_lock(2, 1)";
            var acquired = await lockCmd.ExecuteScalarAsync();
            acquired.Should().Be(true);
        }

        try
        {
            var result = await service.RunAsync(new MaterializeOccurrencesCommand
            {
                TriggerType = "Scheduled",
            });

            result.Status.Should().Be("Failure");
            result.ErrorDetail.Should().Contain("Another materializer run is in progress");
            result.OccurrencesCreated.Should().Be(0);

            var occurrenceCount = await Db.ServiceOccurrences.CountAsync();
            occurrenceCount.Should().Be(0);
        }
        finally
        {
            await using var unlockCmd = externalConnection.CreateCommand();
            unlockCmd.CommandText = "SELECT pg_advisory_unlock(2, 1)";
            await unlockCmd.ExecuteScalarAsync();
        }
    }

    // Matrix row 11.0:59a5667c79c2: A prior run and a concurrent run contending for the advisory lock | Both complete
    [Fact]
    [Trait("MatrixRow", "11.0:59a5667c79c2")]
    public async Task Run_AfterExternalLockReleased_Succeeds()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        await using var externalConnection = new NpgsqlConnection(ConnectionString);
        await externalConnection.OpenAsync();

        await using (var lockCmd = externalConnection.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_try_advisory_lock(2, 1)";
            await lockCmd.ExecuteScalarAsync();
        }

        await using (var unlockCmd = externalConnection.CreateCommand())
        {
            unlockCmd.CommandText = "SELECT pg_advisory_unlock(2, 1)";
            await unlockCmd.ExecuteScalarAsync();
        }

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(4);
    }

    // -----------------------------------------------------------------
    // Acceptance-matrix coverage
    // -----------------------------------------------------------------

    // Matrix row 11.0:3d4f59cfd447: An active schedule whose definition is inactive | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:3d4f59cfd447")]
    public async Task Run_WithInactiveDefinition_DoesNotPickUpSchedule()
    {
        await SeedMinimalConfigurationAsync(definitionActive: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(0);
        result.OccurrencesCreated.Should().Be(0);

        var count = await Db.ServiceOccurrences.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 11.0:ffe380eb2a8a: An active schedule whose time slot is inactive | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:ffe380eb2a8a")]
    public async Task Run_WithInactiveTimeSlot_DoesNotPickUpSchedule()
    {
        await SeedMinimalConfigurationAsync(slotActive: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(0);
        result.OccurrencesCreated.Should().Be(0);

        var count = await Db.ServiceOccurrences.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 11.0:343443820458: A schedule whose ServiceDefinition has zero duties | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:343443820458")]
    public async Task Run_WithZeroDutyDefinition_StillCreatesOccurrences()
    {
        // The fixture seeds no ServiceDefinitionDuty rows, so this is
        // the zero-duty case. The occurrence is still created.
        await SeedMinimalConfigurationAsync();

        var dutyCount = await Db.ServiceDefinitionDuties.CountAsync();
        dutyCount.Should().Be(0);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(4);
    }

    // Matrix row 11.0:b4da0b867eb1: OccurrenceHorizonDays = 0 | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:b4da0b867eb1")]
    public async Task Run_WithHorizonZero_CreatesOnlyTodaysCandidateDate()
    {
        // The clock is set to Sunday, 2026-10-11. With a zero-day
        // horizon, the only candidate date is today, and today is a
        // Sunday, matching the seeded time slot.
        await SeedMinimalConfigurationAsync(horizonDays: 0);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(1);

        var occurrences = await Db.ServiceOccurrences.ToListAsync();
        occurrences.Should().HaveCount(1);
        occurrences.Single().Date.Should().Be(new DateOnly(2026, 10, 11));
    }

    // Matrix row 11.0:f8ff5eed3e50: A Scheduled trigger with a non-null TriggeredByAdminId | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:f8ff5eed3e50")]
    public async Task Run_WithScheduledTriggerAndAdmin_Throws()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var act = async () => await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
            TriggeredByAdminId = 1,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scheduled*TriggeredByAdminId*");
    }

    // Matrix row 11.0:d34683b09f32: A manual horizon override smaller than the existing coverage | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:d34683b09f32")]
    public async Task Run_WithManualHorizonOverrideSmallerThanCoverage_CreatesNoNew()
    {
        await SeedMinimalConfigurationAsync(horizonDays: 30);
        var adminId = await SeedAdminAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var first = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });
        first.OccurrencesCreated.Should().Be(4);

        var manual = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Manual",
            TriggeredByAdminId = adminId,
            HorizonDaysOverride = 7,
        });

        manual.Status.Should().Be("Success");
        manual.OccurrencesCreated.Should().Be(0);

        var count = await Db.ServiceOccurrences.CountAsync();
        count.Should().Be(4);
    }

    // Matrix row 11.0:a455ece91571: A manual horizon override larger than the standing setting | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:a455ece91571")]
    public async Task Run_WithManualHorizonOverrideLargerThanCoverage_CreatesAdditionalOccurrences()
    {
        await SeedMinimalConfigurationAsync(horizonDays: 7);
        var adminId = await SeedAdminAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var first = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });
        first.OccurrencesCreated.Should().Be(1);

        var manual = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Manual",
            TriggeredByAdminId = adminId,
            HorizonDaysOverride = 30,
        });

        manual.Status.Should().Be("Success");
        manual.OccurrencesCreated.Should().Be(3);

        var count = await Db.ServiceOccurrences.CountAsync();
        count.Should().Be(4);
    }

    // Matrix row 11.0:e38857d853c8: An existing schedule-sourced occurrence for a candidate (ScheduleID, Date) | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:e38857d853c8")]
    public async Task Run_WithExistingOccurrence_DoesNotTouchIt()
    {
        await SeedMinimalConfigurationAsync();

        var scheduleId = await Db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();

        // Seed one candidate occurrence directly, with non-default
        // values that 11.0 would never write. If 11.0 touches the row,
        // these values change.
        var adminId = await SeedAdminAsync();
        var seeded = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = adminId,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(seeded);
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(3);

        Db.ChangeTracker.Clear();
        var after = await Db.ServiceOccurrences
            .AsNoTracking()
            .FirstAsync(o => o.OccurrenceId == seeded.OccurrenceId);

        after.GeneratedBy.Should().Be("Administrator");
        after.CreatedBy.Should().Be(adminId);
        after.ChangedBy.Should().BeNull();
        after.Date.Should().Be(new DateOnly(2026, 10, 11));
    }

    // Matrix row 11.0:99d002107f2e: An existing occurrence created as event-sourced | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:99d002107f2e")]
    public async Task Run_WithEventSourcedOccurrence_DoesNotTouchIt()
    {
        await SeedMinimalConfigurationAsync();
        var adminId = await SeedAdminAsync();

        var branchId = await Db.Branches.Select(b => b.BranchId).FirstAsync();
        var evt = new Event
        {
            Name = "Coverage event",
            StartDate = new DateOnly(2026, 10, 11),
            EndDate = new DateOnly(2026, 10, 11),
            Type = "Coverage",
            HostBranchId = branchId,
            IsDeleted = false,
        };
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var eventOccurrence = new ServiceOccurrence
        {
            ScheduleId = null,
            EventId = evt.EventId,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = adminId,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(eventOccurrence);
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(4);

        Db.ChangeTracker.Clear();
        var after = await Db.ServiceOccurrences
            .AsNoTracking()
            .FirstAsync(o => o.OccurrenceId == eventOccurrence.OccurrenceId);

        after.ScheduleId.Should().BeNull();
        after.EventId.Should().Be(evt.EventId);
        after.GeneratedBy.Should().Be("Administrator");
        after.CreatedBy.Should().Be(adminId);
        after.Date.Should().Be(new DateOnly(2026, 10, 11));

        // No new row carries the EventId.
        var otherEventRows = await Db.ServiceOccurrences
            .Where(o => o.EventId == evt.EventId)
            .CountAsync();
        otherEventRows.Should().Be(1);
    }

    // Matrix row 11.0:1941b4368c71: An existing ServiceOccurrenceDuty override on a materialized occurrence | 11.0 runs
    [Fact]
    [Trait("MatrixRow", "11.0:1941b4368c71")]
    public async Task Run_WithServiceOccurrenceDutyOverride_DoesNotModifyIt()
    {
        await SeedMinimalConfigurationAsync();

        var scheduleId = await Db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();
        var duty = new Duty { Name = "Reading duty", IsActive = true, IsDeleted = false };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        Db.ServiceOccurrenceDuties.Add(new ServiceOccurrenceDuty
        {
            OccurrenceId = occurrence.OccurrenceId,
            DutyId = duty.DutyId,
            Action = "Added",
            RequiredSlotCount = 2,
        });
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");

        Db.ChangeTracker.Clear();
        var overrideAfter = await Db.ServiceOccurrenceDuties
            .AsNoTracking()
            .SingleAsync(d => d.OccurrenceId == occurrence.OccurrenceId);

        overrideAfter.Action.Should().Be("Added");
        overrideAfter.RequiredSlotCount.Should().Be(2);

        var overrideCount = await Db.ServiceOccurrenceDuties.CountAsync();
        overrideCount.Should().Be(1);
    }

    // Matrix row 11.0:8bba9710f016: A failed run partway through | 11.0 runs again
    [Fact]
    [Trait("MatrixRow", "11.0:8bba9710f016")]
    public async Task Run_AfterPartialRun_FillsRemainingWithoutDuplicating()
    {
        // This simulates a prior run that created only part of the
        // horizon. The service is deterministic and does not fail
        // midway under test control; the test verifies the recoverable
        // state, not the failure itself. The service must fill the
        // remaining candidate dates without touching the seeded rows.
        await SeedMinimalConfigurationAsync();

        var scheduleId = await Db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();

        var seededFirst = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        var seededSecond = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 18),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(seededFirst);
        Db.ServiceOccurrences.Add(seededSecond);
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(2);

        var occurrences = await Db.ServiceOccurrences
            .OrderBy(o => o.Date)
            .ToListAsync();

        occurrences.Should().HaveCount(4);
        occurrences.Select(o => o.Date).Should().Equal(
            new DateOnly(2026, 10, 11),
            new DateOnly(2026, 10, 18),
            new DateOnly(2026, 10, 25),
            new DateOnly(2026, 11, 1));

        var firstAfter = occurrences.Single(o => o.OccurrenceId == seededFirst.OccurrenceId);
        firstAfter.Date.Should().Be(new DateOnly(2026, 10, 11));

        var secondAfter = occurrences.Single(o => o.OccurrenceId == seededSecond.OccurrenceId);
        secondAfter.Date.Should().Be(new DateOnly(2026, 10, 18));

        var run = await Db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Success");
    }

    // -----------------------------------------------------------------
    // Timezone behaviour
    // -----------------------------------------------------------------

    // Matrix row 11.0:165c4ee05067: An instant near midnight UTC and a configured non-UTC ApplicationTimeZone | 11.0 derives today
    [Fact]
    [Trait("MatrixRow", "11.0:165c4ee05067")]
    public async Task Run_WithNonUtcTimeZone_ResolvesTodayInConfiguredZone()
    {
        // 2026-10-11T22:30:00Z is Sunday in UTC, but 2026-10-12 (Monday)
        // in Africa/Johannesburg (UTC+2). Seed a Monday slot. If 11.0
        // resolved today in UTC, the zero-day horizon window would be
        // [2026-10-11, 2026-10-11], no Monday, and zero occurrences
        // would be created. Resolving today in Johannesburg yields a
        // single occurrence on 2026-10-12. The result therefore
        // demonstrates that materialization used the configured zone.
        await SeedMinimalConfigurationAsync(horizonDays: 0, dayOfWeek: "Monday");

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 22, 30, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(1);

        var occurrences = await Db.ServiceOccurrences.ToListAsync();
        occurrences.Should().HaveCount(1);
        occurrences.Single().Date.Should().Be(new DateOnly(2026, 10, 12));

        var run = await Db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Success");
        run.OccurrencesCreated.Should().Be(1);
    }

    // Matrix row 11.0:248d1a2ae7ce: ApplicationTimeZone set to an unrecognised value | 11.0 derives today
    [Fact]
    [Trait("MatrixRow", "11.0:248d1a2ae7ce")]
    public async Task Run_WithUnrecognisedTimeZone_RecordsFailure()
    {
        await SeedMinimalConfigurationAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "ApplicationTimeZone");
        setting.Value = "Not/ARealTimeZone";
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesCreated.Should().Be(0);
        result.ErrorDetail.Should().NotBeNull();

        var occurrences = await Db.ServiceOccurrences.CountAsync();
        occurrences.Should().Be(0);

        var run = await Db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Failure");
        run.ErrorDetail.Should().NotBeNull();
    }

    // Matrix row 11.0:2ac1faf5c37b: ApplicationTimeZone absent | 11.0 derives today
    [Fact]
    [Trait("MatrixRow", "11.0:2ac1faf5c37b")]
    public async Task Run_WithTimeZoneSettingAbsent_RecordsFailure()
    {
        await SeedMinimalConfigurationAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "ApplicationTimeZone");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(Db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesCreated.Should().Be(0);
        result.ErrorDetail.Should().NotBeNull();

        var occurrences = await Db.ServiceOccurrences.CountAsync();
        occurrences.Should().Be(0);

        var run = await Db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Failure");
        run.ErrorDetail.Should().NotBeNull();
    }

    // Regression test for defect D4: if an occurrence insert fails,
    // the failure MaterializerRun record must still be written. Before
    // the fix, the failed entity stayed tracked and the failure save
    // re-attempted it, throwing and swallowing, so no run record was
    // written. The fix clears the tracker before adding the failure
    // row.
    [Fact]
    public async Task Run_InsertFailure_WritesMaterializerRunFailureRecord()
    {
        await SeedMinimalConfigurationAsync();

        // Seed an existing occurrence for the candidate date, then
        // remove it from the tracker. The materializer's existence
        // check filters by Date range; we insert a row that the
        // read filter will not see because it is inside an
        // uncommitted state that only the unique index will catch.
        //
        // Simpler approach: force a save failure by placing a
        // bad-value row on the schedule. Wrap the insert in a
        // transaction on a second connection that conflicts.
        //
        // The cleanest deterministic approach: hold the
        // (ScheduleId, Date) unique-key slot from an external
        // transaction that has not yet committed.
        var scheduleId = await Db.ServiceSchedules
            .Select(s => s.ScheduleId)
            .FirstAsync();

        await using var blocker = new Npgsql.NpgsqlConnection(ConnectionString);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await using (var cmd = blocker.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO service_occurrence
                    (schedule_id, event_id, date, service_type_id, start_time,
                     fill_status_id, generated_by, created_by, changed_by)
                VALUES (@sid, NULL, @d, NULL, NULL, NULL, 'System', NULL, NULL)";
            cmd.Parameters.AddWithValue("@sid", scheduleId);
            cmd.Parameters.AddWithValue("@d", new DateOnly(2026, 10, 11));
            await cmd.ExecuteNonQueryAsync();
        }
        // Leave the transaction open so the row is visible to the
        // unique index but not to the materializer's read filter
        // (READ COMMITTED default: uncommitted rows are invisible).

        try
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
            var service = new MaterializeOccurrencesService(Db, clock);

            var result = await service.RunAsync(new MaterializeOccurrencesCommand
            {
                TriggerType = "Scheduled",
            });

            result.Status.Should().Be("Failure",
                "the unique-index collision on (schedule_id, date) aborts the run");

            // Wait for the blocking transaction to release before
            // asserting on the MaterializerRun row, so the failure
            // record is not itself blocked.
            await tx.RollbackAsync();

            Db.ChangeTracker.Clear();
            var run = await Db.MaterializerRuns
                .AsNoTracking()
                .OrderByDescending(r => r.RunId)
                .FirstOrDefaultAsync();

            run.Should().NotBeNull(
                "the failure record must be written even when an occurrence insert fails");
            run!.Status.Should().Be("Failure");
            run.ErrorDetail.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            try { await tx.RollbackAsync(); } catch { }
        }
    }
}

/// <summary>
/// Deterministic TimeProvider for tests. Provides a fixed "now" so that
/// date arithmetic is stable across runs.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now;
}
