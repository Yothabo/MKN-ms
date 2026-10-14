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
public class MaterializeOccurrencesTests : IAsyncLifetime
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

        // Clean state for each test.
        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, service_occurrence, service_occurrence_duty, " +
            "service_schedule, branch_time_slot, service_definition, service_type, " +
            "time_of_day, branch, system_setting RESTART IDENTITY CASCADE;");

        await TestSeed.EnsureRequiredSettingsAsync(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Run_WithValidConfiguration_CreatesOccurrencesForAllCandidateDates()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(1);
        result.OccurrencesCreated.Should().Be(4);

        var occurrences = await _db.ServiceOccurrences.OrderBy(o => o.Date).ToListAsync();

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

    [Fact]
    public async Task Run_Twice_IsIdempotent()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var first = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        var countAfterFirst = await _db.ServiceOccurrences.CountAsync();

        var second = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        var countAfterSecond = await _db.ServiceOccurrences.CountAsync();

        first.OccurrencesCreated.Should().Be(4);
        second.OccurrencesCreated.Should().Be(0);
        countAfterSecond.Should().Be(countAfterFirst);

        var runs = await _db.MaterializerRuns.OrderBy(r => r.RunId).ToListAsync();
        runs.Should().HaveCount(2);
        runs.Should().AllSatisfy(r => r.Status.Should().Be("Success"));
    }

    [Fact]
    public async Task Run_WithoutRequiredSetting_RecordsFailure()
    {
        // Seed everything except the required setting.
        await SeedMinimalConfigurationAsync(includeHorizonSetting: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesCreated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OccurrenceHorizonDays");

        var occurrences = await _db.ServiceOccurrences.CountAsync();
        occurrences.Should().Be(0);

        var run = await _db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Failure");
        run.ErrorDetail.Should().Contain("OccurrenceHorizonDays");
    }

    [Fact]
    public async Task Run_WithManualTriggerAndNoAdmin_Throws()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

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
        bool slotActive = true)
    {
        if (includeHorizonSetting)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OccurrenceHorizonDays",
                Value = horizonDays.ToString(),
                Required = true,
            });
        }

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var serviceType = new ServiceType { Name = "Regular" };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };

        _db.TimeOfDays.Add(timeOfDay);
        _db.ServiceTypes.Add(serviceType);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsActive = slotActive,
        };

        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = definitionActive,
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
    }

    private async Task<int> SeedAdminAsync()
    {
        var role = new Role { Name = "Coordinator", IsActive = true, IsDeleted = false };
        var branch = await _db.Branches.FirstAsync();
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(_db);
        var tier = new PermissionTier { Name = "Full Admin", IsDefault = true, IsDeleted = false };

        _db.Roles.Add(role);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

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
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return admin.AdminId;
    }

    [Fact]
    public async Task Run_WhenLockHeldExternally_FailsWithOverlapMessage()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        await using var externalConnection = new NpgsqlConnection(TestConnectionString);
        await externalConnection.OpenAsync();

        await using (var lockCmd = externalConnection.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_try_advisory_lock(1100)";
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

            var occurrenceCount = await _db.ServiceOccurrences.CountAsync();
            occurrenceCount.Should().Be(0);
        }
        finally
        {
            await using var unlockCmd = externalConnection.CreateCommand();
            unlockCmd.CommandText = "SELECT pg_advisory_unlock(1100)";
            await unlockCmd.ExecuteScalarAsync();
        }
    }

    [Fact]
    public async Task Run_AfterExternalLockReleased_Succeeds()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        await using var externalConnection = new NpgsqlConnection(TestConnectionString);
        await externalConnection.OpenAsync();

        await using (var lockCmd = externalConnection.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_try_advisory_lock(1100)";
            await lockCmd.ExecuteScalarAsync();
        }

        await using (var unlockCmd = externalConnection.CreateCommand())
        {
            unlockCmd.CommandText = "SELECT pg_advisory_unlock(1100)";
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

    [Fact]
    public async Task Run_WithInactiveDefinition_DoesNotPickUpSchedule()
    {
        await SeedMinimalConfigurationAsync(definitionActive: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(0);
        result.OccurrencesCreated.Should().Be(0);

        var count = await _db.ServiceOccurrences.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Run_WithInactiveTimeSlot_DoesNotPickUpSchedule()
    {
        await SeedMinimalConfigurationAsync(slotActive: false);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.SchedulesEvaluated.Should().Be(0);
        result.OccurrencesCreated.Should().Be(0);

        var count = await _db.ServiceOccurrences.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Run_WithZeroDutyDefinition_StillCreatesOccurrences()
    {
        // The fixture seeds no ServiceDefinitionDuty rows, so this is
        // the zero-duty case. The occurrence is still created.
        await SeedMinimalConfigurationAsync();

        var dutyCount = await _db.ServiceDefinitionDuties.CountAsync();
        dutyCount.Should().Be(0);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(4);
    }

    [Fact]
    public async Task Run_WithHorizonZero_CreatesOnlyTodaysCandidateDate()
    {
        // The clock is set to Sunday, 2026-10-11. With a zero-day
        // horizon, the only candidate date is today, and today is a
        // Sunday, matching the seeded time slot.
        await SeedMinimalConfigurationAsync(horizonDays: 0);

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(1);

        var occurrences = await _db.ServiceOccurrences.ToListAsync();
        occurrences.Should().HaveCount(1);
        occurrences.Single().Date.Should().Be(new DateOnly(2026, 10, 11));
    }

    [Fact]
    public async Task Run_WithScheduledTriggerAndAdmin_Throws()
    {
        await SeedMinimalConfigurationAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var act = async () => await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
            TriggeredByAdminId = 1,
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Scheduled*TriggeredByAdminId*");
    }

    [Fact]
    public async Task Run_WithManualHorizonOverrideSmallerThanCoverage_CreatesNoNew()
    {
        await SeedMinimalConfigurationAsync(horizonDays: 30);
        var adminId = await SeedAdminAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

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

        var count = await _db.ServiceOccurrences.CountAsync();
        count.Should().Be(4);
    }

    [Fact]
    public async Task Run_WithManualHorizonOverrideLargerThanCoverage_CreatesAdditionalOccurrences()
    {
        await SeedMinimalConfigurationAsync(horizonDays: 7);
        var adminId = await SeedAdminAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

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

        var count = await _db.ServiceOccurrences.CountAsync();
        count.Should().Be(4);
    }

    [Fact]
    public async Task Run_WithExistingOccurrence_DoesNotTouchIt()
    {
        await SeedMinimalConfigurationAsync();

        var scheduleId = await _db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();

        // Seed one candidate occurrence directly, with non-default
        // values that 11.0 would never write. If 11.0 touches the row,
        // these values change.
        var seeded = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = await SeedAdminAsync(),
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(seeded);
        await _db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(3);

        _db.ChangeTracker.Clear();
        var after = await _db.ServiceOccurrences
            .AsNoTracking()
            .FirstAsync(o => o.OccurrenceId == seeded.OccurrenceId);

        after.GeneratedBy.Should().Be("Administrator");
        after.CreatedBy.Should().Be(seeded.CreatedBy);
        after.ChangedBy.Should().BeNull();
        after.Date.Should().Be(new DateOnly(2026, 10, 11));
    }

    [Fact]
    public async Task Run_WithEventSourcedOccurrence_DoesNotTouchIt()
    {
        await SeedMinimalConfigurationAsync();
        var adminId = await SeedAdminAsync();

        var branchId = await _db.Branches.Select(b => b.BranchId).FirstAsync();
        var evt = new Event
        {
            Name = "Coverage event",
            StartDate = new DateOnly(2026, 10, 11),
            EndDate = new DateOnly(2026, 10, 11),
            Type = "Coverage",
            HostBranchId = branchId,
            IsDeleted = false,
        };
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();

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
        _db.ServiceOccurrences.Add(eventOccurrence);
        await _db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(4);

        _db.ChangeTracker.Clear();
        var after = await _db.ServiceOccurrences
            .AsNoTracking()
            .FirstAsync(o => o.OccurrenceId == eventOccurrence.OccurrenceId);

        after.ScheduleId.Should().BeNull();
        after.EventId.Should().Be(evt.EventId);
        after.GeneratedBy.Should().Be("Administrator");
        after.CreatedBy.Should().Be(adminId);
        after.Date.Should().Be(new DateOnly(2026, 10, 11));

        // No new row carries the EventId.
        var otherEventRows = await _db.ServiceOccurrences
            .Where(o => o.EventId == evt.EventId)
            .CountAsync();
        otherEventRows.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithServiceOccurrenceDutyOverride_DoesNotModifyIt()
    {
        await SeedMinimalConfigurationAsync();

        var scheduleId = await _db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();
        var duty = new Duty { Name = "Reading duty", IsActive = true, IsDeleted = false };
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

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
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        _db.ServiceOccurrenceDuties.Add(new ServiceOccurrenceDuty
        {
            OccurrenceId = occurrence.OccurrenceId,
            DutyId = duty.DutyId,
            Action = "Added",
            RequiredSlotCount = 2,
        });
        await _db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");

        _db.ChangeTracker.Clear();
        var overrideAfter = await _db.ServiceOccurrenceDuties
            .AsNoTracking()
            .SingleAsync(d => d.OccurrenceId == occurrence.OccurrenceId);

        overrideAfter.Action.Should().Be("Added");
        overrideAfter.RequiredSlotCount.Should().Be(2);

        var overrideCount = await _db.ServiceOccurrenceDuties.CountAsync();
        overrideCount.Should().Be(1);
    }

    [Fact]
    public async Task Run_AfterPartialRun_FillsRemainingWithoutDuplicating()
    {
        // This simulates a prior run that created only part of the
        // horizon. The service is deterministic and does not fail
        // midway under test control; the test verifies the recoverable
        // state, not the failure itself. The service must fill the
        // remaining candidate dates without touching the seeded rows.
        await SeedMinimalConfigurationAsync();

        var scheduleId = await _db.ServiceSchedules.Select(s => s.ScheduleId).FirstAsync();

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
        _db.ServiceOccurrences.Add(seededFirst);
        _db.ServiceOccurrences.Add(seededSecond);
        await _db.SaveChangesAsync();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));
        var service = new MaterializeOccurrencesService(_db, clock);

        var result = await service.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.OccurrencesCreated.Should().Be(2);

        var occurrences = await _db.ServiceOccurrences
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

        var run = await _db.MaterializerRuns.SingleAsync();
        run.Status.Should().Be("Success");
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
