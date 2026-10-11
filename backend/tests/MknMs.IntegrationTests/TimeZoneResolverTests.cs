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
/// §15.3.11: the resolver does not fall back. An absent setting
/// produces ConfigurationMissing; an unrecognised value produces
/// ConfigurationInvalid. A configured valid value is returned.
///
/// Reference: §7, §15.3.11, §15.8.33.
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
            Required = true,
        });
        await _db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(_db);

        result.IsSuccess.Should().BeTrue();
        result.Zone!.Id.Should().Be("Africa/Johannesburg");
    }

    [Fact]
    public async Task Resolve_WithoutConfiguredTimeZone_ReturnsConfigurationMissing()
    {
        var result = await TimeZoneResolver.ResolveAsync(_db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ConfigurationErrorCodes.ConfigurationMissing);
    }

    [Fact]
    public async Task Resolve_WithUnrecognisedValue_ReturnsConfigurationInvalid()
    {
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Not/ARealTimeZone",
            Required = true,
        });
        await _db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(_db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ConfigurationErrorCodes.ConfigurationInvalid);
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
            Required = true,
        });
        await _db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(_db);
        result.IsSuccess.Should().BeTrue();

        var date = TimeZoneResolver.ToDateInTimeZone(instant, result.Zone!);

        date.Should().Be(new DateOnly(2026, 10, 7));
    }
}
