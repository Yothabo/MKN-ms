using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.Lookup_Capability;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for CapabilityService (D10 lookup, create and list only).
/// </summary>
public class CapabilityServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task List_ReturnsNonDeletedOrdered()
    {
        Db.Capabilities.Add(new Capability { Name = "Beta" });
        Db.Capabilities.Add(new Capability { Name = "Alpha" });
        await Db.SaveChangesAsync();

        var service = new CapabilityService(Db);
        var result = await service.ListAsync();

        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().Equal("Beta", "Alpha");
    }

    [Fact]
    public async Task List_ExcludesSoftDeleted()
    {
        Db.Capabilities.Add(new Capability { Name = "Live" });
        Db.Capabilities.Add(new Capability
        {
            Name = "Gone", IsActive = false, IsDeleted = true,
        });
        await Db.SaveChangesAsync();

        var service = new CapabilityService(Db);
        var result = await service.ListAsync();

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Live");
    }

    [Fact]
    public async Task Create_WithNameAndDescription_WritesRow()
    {
        var service = new CapabilityService(Db);

        var result = await service.CreateAsync(
            "ManageRoster", "Create and edit roster assignments.");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("ManageRoster");
        result.Value.Description.Should().Be("Create and edit roster assignments.");
        result.Value.IsActive.Should().BeTrue();
        result.Value.IsDeleted.Should().BeFalse();

        var persisted = await Db.Capabilities.AsNoTracking()
            .FirstAsync(c => c.CapabilityId == result.Value.CapabilityId);
        persisted.Name.Should().Be("ManageRoster");
    }

    [Fact]
    public async Task Create_WithBlankName_Fails()
    {
        var service = new CapabilityService(Db);

        var result = await service.CreateAsync("   ", null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);

        var count = await Db.Capabilities.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Create_SetsBothFlagsExplicitly()
    {
        var service = new CapabilityService(Db);

        var result = await service.CreateAsync("Act", null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
        result.Value.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithNullDescription_WritesNullDescription()
    {
        var service = new CapabilityService(Db);

        var result = await service.CreateAsync("ReadOnly", null);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().BeNull();
    }
}
