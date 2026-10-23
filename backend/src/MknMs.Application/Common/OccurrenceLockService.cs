
using System.Data;
using Npgsql;

namespace MknMs.Application.Common;

/// <summary>
/// A session-level advisory lock on an occurrence. Held for the
/// lifetime of the lease and released in DisposeAsync on the same
/// PostgreSQL session that acquired it.
/// </summary>
public interface IOccurrenceLockLease : IAsyncDisposable
{
}

/// <summary>
/// Acquires a PostgreSQL session-level advisory lock on an
/// occurrence, used to protect the slot-capacity invariant (§10.3,
/// §11.3, B6) against concurrent writers.
/// </summary>
/// <remarks>
/// The three writers that can change a slot's occupancy are 5.0
/// Generate Assignment, 7.0 Manage Confirmation (re-resolution), and
/// 12.0 Create Manual Assignment. All three acquire the same lock for
/// the same OccurrenceId, so any interleaving of their
/// capacity-sensitive work is serialized.
///
/// The lock is coarser than the strict slot (OccurrenceId, DutyId)
/// granularity: two writers on the same occurrence but different
/// duties also serialize. That is a deliberate choice — the read that
/// feeds the capacity check is per-occurrence, and the coarser lock
/// is strictly stronger than the required property.
///
/// TryAcquireAsync returns null on contention. A caller that gets
/// null must not proceed with the capacity-sensitive work; it
/// reports a conflict and lets the operation be retried.
///
/// The lease retains the connection on which the lock was acquired.
/// DisposeAsync releases the lock and, if this helper opened the
/// connection, closes it. If unlocking fails, closing the connection
/// releases the session-level lock as a fallback.
/// </remarks>
public interface IOccurrenceLockService
{
    Task<IOccurrenceLockLease?> TryAcquireAsync(
        int occurrenceId,
        CancellationToken cancellationToken = default);
}

public sealed class OccurrenceLockService : IOccurrenceLockService
{
    /// <summary>
    /// PostgreSQL advisory lock class ID for occurrence locks. The
    /// materializer uses class ID 2 for its mutual-exclusion lock;
    /// class ID 1 and class ID 2 are disjoint keyspaces, so an
    /// occurrence lock on OccurrenceId 1100 cannot collide with the
    /// materializer's lock. See MaterializeOccurrencesService for the
    /// materializer's class constant.
    /// </summary>
    internal const int LockClassId = 1;
    private readonly MknDbContext _db;

    public OccurrenceLockService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IOccurrenceLockLease?> TryAcquireAsync(
        int occurrenceId,
        CancellationToken cancellationToken = default)
    {
        if (occurrenceId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurrenceId),
                "OccurrenceId must be positive.");
        }

        var connection = _db.Database.GetDbConnection();
        var openedByHelper = connection.State != ConnectionState.Open;

        if (openedByHelper)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(1, @key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@key";
            parameter.Value = occurrenceId;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var acquired = result is bool b && b;

            if (!acquired)
            {
                if (openedByHelper && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }

                return null;
            }

            return new OccurrenceLockLease(
                connection, occurrenceId, openedByHelper);
        }
        catch
        {
            if (openedByHelper && connection.State == ConnectionState.Open)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch
                {
                    // Best-effort cleanup; the original exception
                    // propagates.
                }
            }

            throw;
        }
    }

    private sealed class OccurrenceLockLease : IOccurrenceLockLease
    {
        private readonly System.Data.Common.DbConnection _connection;
        private readonly int _occurrenceId;
        private readonly bool _openedByHelper;
        private bool _disposed;

        public OccurrenceLockLease(
            System.Data.Common.DbConnection connection,
            int occurrenceId,
            bool openedByHelper)
        {
            _connection = connection;
            _occurrenceId = occurrenceId;
            _openedByHelper = openedByHelper;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            // Release the advisory lock on the same session. If the
            // release fails, closing the connection below releases
            // the session-level lock as a fallback.
            try
            {
                if (_connection.State == ConnectionState.Open)
                {
                    await using var command = _connection.CreateCommand();
                    command.CommandText = "SELECT pg_advisory_unlock(1, @key)";
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@key";
                    parameter.Value = _occurrenceId;
                    command.Parameters.Add(parameter);
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            catch
            {
                // Best-effort; closing the connection is the fallback.
            }

            if (_openedByHelper && _connection.State == ConnectionState.Open)
            {
                try
                {
                    await _connection.CloseAsync();
                }
                catch
                {
                    // Nothing further can be done.
                }
            }
        }
    }
}
