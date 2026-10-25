using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Configuration.OccurrenceRead;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for OccurrenceReadService.
/// </summary>
public class OccurrenceReadServiceTests : IntegrationTestBase
{
    private async Task<(int ScheduleId, int ServiceTypeId)> SeedScheduleAsync()
    {
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var serviceType = new ServiceType { Name = "Regular" };
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };

        Db.TimeOfDays.Add(timeOfDay);
        Db.ServiceTypes.Add(serviceType);
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S",
            ServiceTypeId = serviceType.ServiceTypeId,
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

        return (schedule.ScheduleId, serviceType.ServiceTypeId);
    }

    private async Task AddOccurrenceAsync(
        int scheduleId, int serviceTypeId, DateOnly date)
    {
        Db.ServiceOccurrences.Add(new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = date,
            ServiceTypeId = serviceTypeId,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        });
        await Db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_NoFilters_ReturnsAllOrderedByDateThenId()
    {
        var (scheduleId, serviceTypeId) = await SeedScheduleAsync();
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 15));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 8));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 1));

        var service = new OccurrenceReadService(Db);
        var all = await service.ListAsync();

        all.Should().HaveCount(3);
        all.Select(o => o.Date).Should().Equal(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 8),
            new DateOnly(2026, 11, 15));
    }

    [Fact]
    public async Task List_FromDateFilter_ReturnsOnlyOnOrAfter()
    {
        var (scheduleId, serviceTypeId) = await SeedScheduleAsync();
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 1));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 8));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 15));

        var service = new OccurrenceReadService(Db);
        var filtered = await service.ListAsync(fromDate: new DateOnly(2026, 11, 8));

        filtered.Should().HaveCount(2);
        filtered.Select(o => o.Date).Should().Equal(
            new DateOnly(2026, 11, 8),
            new DateOnly(2026, 11, 15));
    }

    [Fact]
    public async Task List_ToDateFilter_ReturnsOnlyOnOrBefore()
    {
        var (scheduleId, serviceTypeId) = await SeedScheduleAsync();
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 1));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 8));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 15));

        var service = new OccurrenceReadService(Db);
        var filtered = await service.ListAsync(toDate: new DateOnly(2026, 11, 8));

        filtered.Should().HaveCount(2);
        filtered.Select(o => o.Date).Should().Equal(
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 8));
    }

    [Fact]
    public async Task List_BothFilters_ReturnsInRangeInclusive()
    {
        var (scheduleId, serviceTypeId) = await SeedScheduleAsync();
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 1));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 8));
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 15));

        var service = new OccurrenceReadService(Db);
        var filtered = await service.ListAsync(
            fromDate: new DateOnly(2026, 11, 8),
            toDate: new DateOnly(2026, 11, 8));

        filtered.Should().HaveCount(1);
        filtered.Single().Date.Should().Be(new DateOnly(2026, 11, 8));
    }

    [Fact]
    public async Task List_NoMatchingRows_ReturnsEmpty()
    {
        var (scheduleId, serviceTypeId) = await SeedScheduleAsync();
        await AddOccurrenceAsync(scheduleId, serviceTypeId, new DateOnly(2026, 11, 1));

        var service = new OccurrenceReadService(Db);
        var filtered = await service.ListAsync(
            fromDate: new DateOnly(2030, 1, 1),
            toDate: new DateOnly(2030, 12, 31));

        filtered.Should().BeEmpty();
    }
}
