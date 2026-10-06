using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D12_MaterializerRun;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D10_ConfigLookups;
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

    private async Task SeedMinimalConfigurationAsync(bool includeHorizonSetting = true)
    {
        if (includeHorizonSetting)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "OccurrenceHorizonDays",
                Value = "30",
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
            IsActive = true,
        };

        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };

        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
        };

        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();
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
