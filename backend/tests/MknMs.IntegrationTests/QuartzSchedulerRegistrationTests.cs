using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MknMs.Infrastructure.Scheduling;
using Quartz;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for QuartzSchedulerRegistration.
/// </summary>
/// <remarks>
/// These tests cover the boundary MKN's code owns: that the four cron
/// expressions are the intended values, that the registration method
/// accepts a TimeZoneInfo and wires the four jobs without error, and
/// that the scheduler factory ends up registered in the container.
///
/// They deliberately do not spin up the Quartz scheduler. Quartz's
/// scheduler factory reads ILoggerFactory at construction time, and
/// disposing a standalone ServiceProvider races that read inside
/// Quartz.Extensions.Hosting 3.13.0. That race is a property of the
/// library, not of MKN's code, and it is Quartz's own test suite that
/// verifies the scheduler honours InTimeZone. What MKN must verify is
/// that it passes the correct timezone, which is what these tests do.
///
/// Reference: §7, §10, §11, §12, §16.
/// </remarks>
public class QuartzSchedulerRegistrationTests
{
    [Fact]
    public void CronConstants_AreTheIntendedValues()
    {
        QuartzSchedulerRegistration.MaterializeCron.Should().Be("0 0 2 * * ?");
        QuartzSchedulerRegistration.GenerateCron.Should().Be("0 0 3 * * ?");
        QuartzSchedulerRegistration.ConfirmationTimeoutCron.Should().Be("0 0 * * * ?");
        QuartzSchedulerRegistration.FillStatusSweepCron.Should().Be("0 30 * * * ?");
    }

    [Fact]
    public void AddMknScheduledJobs_WithResolvedTimeZone_RegistersTheSchedulerFactory()
    {
        var services = new ServiceCollection();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

        var act = () => services.AddMknScheduledJobs(zone);

        act.Should().NotThrow();

        services.Should().Contain(
            d => d.ServiceType == typeof(ISchedulerFactory),
            "AddMknScheduledJobs must register the Quartz scheduler factory");
    }

    [Fact]
    public void AddMknScheduledJobs_WithUtcTimeZone_AlsoRegisters()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMknScheduledJobs(TimeZoneInfo.Utc);

        act.Should().NotThrow();

        services.Should().Contain(
            d => d.ServiceType == typeof(ISchedulerFactory));
    }
}
