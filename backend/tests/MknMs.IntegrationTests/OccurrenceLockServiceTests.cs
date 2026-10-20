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
public class OccurrenceLockServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Acquire_OnFreeOccurrence_ReturnsLease()
    {
        await using var dbA = CreateContext();
        var service = new OccurrenceLockService(dbA);

        await using var lease = await service.TryAcquireAsync(9001);

        lease.Should().NotBeNull();
    }

    [Fact]
    public async Task Acquire_TwiceOnSameOccurrence_SecondReturnsNull()
    {
        await using var dbA = CreateContext();
        await using var dbB = CreateContext();
        var serviceA = new OccurrenceLockService(dbA);
        var serviceB = new OccurrenceLockService(dbB);

        await using var leaseA = await serviceA.TryAcquireAsync(9002);
        leaseA.Should().NotBeNull();

        var leaseB = await serviceB.TryAcquireAsync(9002);
        leaseB.Should().BeNull(
            "a second writer on the same occurrence must not acquire the lock");
    }

    [Fact]
    public async Task Release_ThenReacquire_Succeeds()
    {
        await using var dbA = CreateContext();
        await using var dbB = CreateContext();
        var serviceA = new OccurrenceLockService(dbA);
        var serviceB = new OccurrenceLockService(dbB);

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
        await using var dbA = CreateContext();
        await using var dbB = CreateContext();
        var serviceA = new OccurrenceLockService(dbA);
        var serviceB = new OccurrenceLockService(dbB);

        await using var leaseA = await serviceA.TryAcquireAsync(9004);
        await using var leaseB = await serviceB.TryAcquireAsync(9005);

        leaseA.Should().NotBeNull();
        leaseB.Should().NotBeNull(
            "locks on different occurrences are independent");
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        await using var dbA = CreateContext();
        var service = new OccurrenceLockService(dbA);
        var lease = await service.TryAcquireAsync(9006);
        lease.Should().NotBeNull();

        await lease!.DisposeAsync();
        await lease.DisposeAsync();  // must not throw

        await using var second = await service.TryAcquireAsync(9006);
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task Acquire_WithNonPositiveId_Throws()
    {
        await using var dbA = CreateContext();
        var service = new OccurrenceLockService(dbA);

        var act = async () => await service.TryAcquireAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
