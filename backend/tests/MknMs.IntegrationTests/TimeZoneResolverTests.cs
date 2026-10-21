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
public class TimeZoneResolverTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // Each fact in this class writes ApplicationTimeZone and asserts on
        // its presence or absence. Clear SystemSetting rows before each fact
        // so the facts are order-independent and do not collide on the key.
        Db.SystemSettings.RemoveRange(Db.SystemSettings);
        await Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Resolve_WithConfiguredTimeZone_ReturnsThatZone()
    {
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = true,
        });
        await Db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(Db);

        result.IsSuccess.Should().BeTrue();
        result.Zone!.Id.Should().Be("Africa/Johannesburg");
    }

    [Fact]
    public async Task Resolve_WithoutConfiguredTimeZone_ReturnsConfigurationMissing()
    {
        var result = await TimeZoneResolver.ResolveAsync(Db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ConfigurationErrorCodes.ConfigurationMissing);
    }

    [Fact]
    public async Task Resolve_WithUnrecognisedValue_ReturnsConfigurationInvalid()
    {
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Not/ARealTimeZone",
            Required = true,
        });
        await Db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(Db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ConfigurationErrorCodes.ConfigurationInvalid);
    }

    // Matrix row Temporal:26f9f2fb0ad6: `ApplicationTimeZone = Africa/Johannesburg` and an instant at 22:30 UTC | The date is derived
    [Fact]
    [Trait("MatrixRow", "Temporal:26f9f2fb0ad6")]
    public async Task ToDateInTimeZone_NearMidnight_ResolvesToCorrectDate()
    {
        // Johannesburg is UTC+2. An instant at 22:30 UTC on the 6th is
        // 00:30 on the 7th in Johannesburg — the next day.
        var instant = new DateTimeOffset(2026, 10, 6, 22, 30, 0, TimeSpan.Zero);

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = true,
        });
        await Db.SaveChangesAsync();

        var result = await TimeZoneResolver.ResolveAsync(Db);
        result.IsSuccess.Should().BeTrue();

        var date = TimeZoneResolver.ToDateInTimeZone(instant, result.Zone!);

        date.Should().Be(new DateOnly(2026, 10, 7));
    }
}
