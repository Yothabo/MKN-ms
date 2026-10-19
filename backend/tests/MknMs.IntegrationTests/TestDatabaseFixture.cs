using Microsoft.EntityFrameworkCore;
using MknMs.Persistence;
using Npgsql;

namespace MknMs.IntegrationTests;

/// <summary>
/// Per-class database fixture. Each test class that uses this fixture
/// gets its own database — created empty, migrated to the current
/// schema, dropped cleanly on disposal.
/// </summary>
/// <remarks>
/// Replaces the shared <c>mkn_test</c> + per-class TRUNCATE pattern.
/// That pattern had three failure modes the fixture removes by
/// construction:
///
///   - a Postgres stall during any class's TRUNCATE cascades into
///     every subsequent class's run;
///   - the same connection pool is shared across classes, so a
///     terminated backend in one class can leave the next class
///     staring at a hung DROP;
///   - a test that wanted a specific empty schema had to be sure
///     its TRUNCATE list was complete.
///
/// The fixture is registered per class via
/// <see cref="Xunit.IClassFixture{TFixture}"/> on
/// <see cref="IntegrationTestBase"/>. With the assembly-level
/// <c>DisableTestParallelization</c> in place, at most one fixture
/// is alive at a time — one database per class, dropped before the
/// next class's fixture is created.
///
/// The <c>AdminConnectionString</c> and the disposal dance
/// (ClearAllPools, pg_terminate_backend, DROP) are the same pattern
/// <c>MigrationSchemaTests</c> already used for its scratch
/// database; this fixture is the generalisation of that pattern to
/// the whole suite.
/// </remarks>
public sealed class TestDatabaseFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=mkn_dev";

    private const string Prefix = "mkn_test_";

    private string _databaseName = null!;
    private MknDbContext _db = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _databaseName = Prefix + Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE {_databaseName}";
            await cmd.ExecuteNonQueryAsync();
        }

        ConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={_databaseName};Username=mkn_dev";

        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        _db = new MknDbContext(options);

        await _db.Database.MigrateAsync();
    }

    /// <summary>
    /// Create a fresh <see cref="MknDbContext"/> against this class's
    /// database. The caller owns the context and must dispose it.
    /// </summary>
    public MknDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new MknDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        // Npgsql pools physical connections. After disposing the
        // context, the underlying connection returns to the pool
        // still open against the per-class database, and DROP
        // DATABASE would fail against it. Clear the pool first; the
        // pg_terminate_backend then finds nothing on the per-class
        // database and DROP succeeds.
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        await using (var terminate = admin.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                + "WHERE datname = @db AND pid <> pg_backend_pid()";
            terminate.Parameters.AddWithValue("@db", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        // Termux Postgres occasionally stalls during DROP DATABASE. The
        // command is accepted and then the read times out. A bounded retry
        // usually succeeds because the stalled backend was terminated by
        // pg_terminate_backend above. On final failure, leave the database
        // rather than fail the test class: a leaked per-class DB is a
        // lesser cost than a red suite for an infrastructure stall.
        const int maxDropAttempts = 3;
        for (var attempt = 1; attempt <= maxDropAttempts; attempt++)
        {
            try
            {
                await using var drop = admin.CreateCommand();
                drop.CommandText = $"DROP DATABASE IF EXISTS {_databaseName}";
                drop.CommandTimeout = 15;
                await drop.ExecuteNonQueryAsync();
                return;
            }
            catch (Exception) when (attempt < maxDropAttempts)
            {
                NpgsqlConnection.ClearAllPools();
                await Task.Delay(TimeSpan.FromSeconds(2));

                await using var terminate = admin.CreateCommand();
                terminate.CommandText =
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                    + "WHERE datname = @db AND pid <> pg_backend_pid()";
                terminate.Parameters.AddWithValue("@db", _databaseName);
                try { await terminate.ExecuteNonQueryAsync(); }
                catch { /* best-effort before retry */ }
            }
            catch (Exception)
            {
                // Final attempt failed. Leave the database; a subsequent
                // InitializeAsync on a different class uses a new GUID-named
                // database and is unaffected.
            }
        }
    }
}
