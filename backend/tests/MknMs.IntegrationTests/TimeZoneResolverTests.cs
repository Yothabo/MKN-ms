using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for TimeZoneResolver.
/// </summary>
/// <remarks>
/// Proves the two behaviours the resolver must have:
///   - A configured ApplicationTimeZone is respected when it is present.
///   - The resolver falls back to UTC when the setting is absent.
///
/// Reference: §7 — "using the configured system/application timezone."
/// </remarks>
public class TimeZoneResolverTests : IAsyncLifetime
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
            "duty_rule, admin, member, role, duty, system_setting, " +
            "outcome_state, assignment_status, permission_tier RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Resolve_WithConfiguredTimeZone_ReturnsThatZone()
    {
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = false,
        });
        await _db.SaveChangesAsync();

        var tz = await TimeZoneResolver.ResolveAsync(_db);

        tz.Id.Should().Be("Africa/Johannesburg");
    }

    [Fact]
    public async Task Resolve_WithoutConfiguredTimeZone_FallsBackToUtc()
    {
        var tz = await TimeZoneResolver.ResolveAsync(_db);

        tz.Id.Should().Be("UTC");
    }

    [Fact]
    public async Task Resolve_WithUnrecognisedValue_FallsBackToUtc()
    {
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Not/ARealTimeZone",
            Required = false,
        });
        await _db.SaveChangesAsync();

        var tz = await TimeZoneResolver.ResolveAsync(_db);

        tz.Id.Should().Be("UTC");
    }

    [Fact]
    public async Task ToDateInTimeZone_NearMidnight_ResolvesToCorrectDate()
    {
        // Johannesburg is UTC+2. An instant at 22:30 UTC on the 6th is
        // 00:30 on the 7th in Johannesburg — the next day.
        var instant = new DateTimeOffset(2026, 10, 6, 22, 30, 0, TimeSpan.Zero);

        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = false,
        });
        await _db.SaveChangesAsync();

        var tz = await TimeZoneResolver.ResolveAsync(_db);
        var date = TimeZoneResolver.ToDateInTimeZone(instant, tz);

        date.Should().Be(new DateOnly(2026, 10, 7));
    }
}
