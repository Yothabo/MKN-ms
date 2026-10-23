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

    // Regression test for defect D1: the materializer's advisory lock
    // uses class 2, object 1; occurrence locks use class 1, object
    // = occurrence id. Before the fix, both used the single-bigint
    // form, and occurrence #1100 collided with the materializer.
    // This test acquires the materializer's lock on a bare connection
    // and then acquires an occurrence lock on OccurrenceId = 1100 from
    // the service. Both must succeed — they are in disjoint keyspaces.
    [Fact]
    public async Task Acquire_MaterializerAndOccurrence_DoNotCollide()
    {
        const int collidingOccurrenceId = 1100;

        // Acquire the materializer's lock (class 2, obj 1) on a bare
        // connection held for the duration of this test.
        await using var materializerConn = new Npgsql.NpgsqlConnection(ConnectionString);
        await materializerConn.OpenAsync();
        await using (var cmd = materializerConn.CreateCommand())
        {
            cmd.CommandText = "SELECT pg_try_advisory_lock(2, 1)";
            var acquired = await cmd.ExecuteScalarAsync();
            acquired.Should().Be(true);
        }

        try
        {
            await using var dbA = CreateContext();
            var service = new OccurrenceLockService(dbA);

            // Before the fix: the single-bigint form would have seen
            // 1100 == 1100 and returned null. After: class 1 and
            // class 2 are disjoint, so this acquires.
            await using var lease = await service.TryAcquireAsync(collidingOccurrenceId);
            lease.Should().NotBeNull(
                "occurrence locks are in advisory-lock class 1; the " +
                "materializer uses class 2; they cannot collide");
        }
        finally
        {
            await using var cmd = materializerConn.CreateCommand();
            cmd.CommandText = "SELECT pg_advisory_unlock(2, 1)";
            await cmd.ExecuteScalarAsync();
        }
    }

    // Regression test for defect D1: the release must use the same
    // two-int form as the acquire. A mismatched release silently
    // returns false and leaves the session-level lock held on a
    // pooled connection. This test acquires, releases, and re-acquires
    // on a fresh context against the same occurrence id. If the
    // release is mismatched, the re-acquire returns null.
    [Fact]
    public async Task Release_TwoIntForm_ReturnsTrue()
    {
        const int occurrenceId = 1101;

        await using var dbA = CreateContext();
        var serviceA = new OccurrenceLockService(dbA);

        var leaseA = await serviceA.TryAcquireAsync(occurrenceId);
        leaseA.Should().NotBeNull();
        await leaseA!.DisposeAsync();

        // Fresh context, same occurrence id. If the release was
        // mismatched (single-int vs two-int), the lock is still held
        // by the pooled connection, and this acquire returns null.
        await using var dbB = CreateContext();
        var serviceB = new OccurrenceLockService(dbB);

        await using var leaseB = await serviceB.TryAcquireAsync(occurrenceId);
        leaseB.Should().NotBeNull(
            "the release must use pg_advisory_unlock(1, id), matching " +
            "pg_try_advisory_lock(1, id); a single-int release is " +
            "silently ignored by PostgreSQL");
    }
}
