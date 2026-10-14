using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for OccurrenceLockService. The lock is a PostgreSQL
/// session-level advisory lock; verifying it requires a real
/// database session, not a mock.
/// </summary>
public class OccurrenceLockServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _dbA = null!;
    private MknDbContext _dbB = null!;

    public Task InitializeAsync()
    {
        var optionsA = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        var optionsB = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _dbA = new MknDbContext(optionsA);
        _dbB = new MknDbContext(optionsB);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dbA.DisposeAsync();
        await _dbB.DisposeAsync();
    }

    [Fact]
    public async Task Acquire_OnFreeOccurrence_ReturnsLease()
    {
        var service = new OccurrenceLockService(_dbA);

        await using var lease = await service.TryAcquireAsync(9001);

        lease.Should().NotBeNull();
    }

    [Fact]
    public async Task Acquire_TwiceOnSameOccurrence_SecondReturnsNull()
    {
        var serviceA = new OccurrenceLockService(_dbA);
        var serviceB = new OccurrenceLockService(_dbB);

        await using var leaseA = await serviceA.TryAcquireAsync(9002);
        leaseA.Should().NotBeNull();

        var leaseB = await serviceB.TryAcquireAsync(9002);
        leaseB.Should().BeNull(
            "a second writer on the same occurrence must not acquire the lock");
    }

    [Fact]
    public async Task Release_ThenReacquire_Succeeds()
    {
        var serviceA = new OccurrenceLockService(_dbA);
        var serviceB = new OccurrenceLockService(_dbB);

        var leaseA = await serviceA.TryAcquireAsync(9003);
        leaseA.Should().NotBeNull();
        await leaseA!.DisposeAsync();

        await using var leaseB = await serviceB.TryAcquireAsync(9003);
        leaseB.Should().NotBeNull(
            "after the first lease releases, a second writer may acquire");
    }

    [Fact]
    public async Task Acquire_DifferentOccurrences_DoNotBlockEachOther()
    {
        var serviceA = new OccurrenceLockService(_dbA);
        var serviceB = new OccurrenceLockService(_dbB);

        await using var leaseA = await serviceA.TryAcquireAsync(9004);
        await using var leaseB = await serviceB.TryAcquireAsync(9005);

        leaseA.Should().NotBeNull();
        leaseB.Should().NotBeNull(
            "locks on different occurrences are independent");
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var service = new OccurrenceLockService(_dbA);
        var lease = await service.TryAcquireAsync(9006);
        lease.Should().NotBeNull();

        await lease!.DisposeAsync();
        await lease.DisposeAsync();  // must not throw

        // A second acquisition on the same occurrence succeeds.
        await using var second = await service.TryAcquireAsync(9006);
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task Acquire_WithNonPositiveId_Throws()
    {
        var service = new OccurrenceLockService(_dbA);

        var act = async () => await service.TryAcquireAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
