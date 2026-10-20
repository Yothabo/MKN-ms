
using Microsoft.EntityFrameworkCore;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Npgsql;

namespace MknMs.IntegrationTests;

/// <summary>
/// Small helpers shared by test fixtures.
/// </summary>
/// <remarks>
/// Member.MemberStatusId is non-null as of migration Step 7. Every
/// fixture that creates a Member must supply a MemberStatusId. This
/// helper ensures a default MemberStatus row exists and returns its
/// id.
/// </remarks>
internal static class TestSeed
{
    public static async Task<int> EnsureDefaultMemberStatusAsync(MknDbContext db)
    {
        var existing = await db.MemberStatuses
            .FirstOrDefaultAsync(s => s.IsDefault);

        if (existing is not null)
        {
            return existing.MemberStatusId;
        }

        var status = new MemberStatus
        {
            Name = "Active",
            IsRosterable = true,
            IsDefault = true,
            IsDeleted = false,
        };
        db.MemberStatuses.Add(status);
        await db.SaveChangesAsync();
        return status.MemberStatusId;
    }

    public static async Task EnsureRequiredSettingsAsync(MknDbContext db)
    {
        if (!await db.SystemSettings.AnyAsync(s => s.Key == "ApplicationTimeZone"))
        {
            db.SystemSettings.Add(new MknMs.Domain.D11_SystemSetting.SystemSetting
            {
                Key = "ApplicationTimeZone",
                Value = "Africa/Johannesburg",
                Required = true,
            });
        }

        if (!await db.SystemSettings.AnyAsync(s => s.Key == "AttendanceRegisterEnabled"))
        {
            db.SystemSettings.Add(new MknMs.Domain.D11_SystemSetting.SystemSetting
            {
                Key = "AttendanceRegisterEnabled",
                Value = "true",
                Required = true,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Base connection string for the test-side admin and per-class
    /// connections. Reads MKN_TEST_ADMIN_CONNECTION from the environment
    /// with the development default as fallback.
    /// </summary>
    /// <remarks>
    /// The value must NOT carry a <c>Database=</c> key. Callers set
    /// <see cref="NpgsqlConnectionStringBuilder.Database"/> per use: the
    /// admin connection uses <c>postgres</c>, the per-class connection
    /// uses the class's own database name. A CI runner sets
    /// <c>MKN_TEST_ADMIN_CONNECTION</c> to point at its postgres service;
    /// the developer environment falls back to the local Termux postgres.
    ///
    /// The variable's value is a full connection string. That is the
    /// right shape for CI: one variable carries every non-database
    /// property (host, port, user, password, SSL mode, timeout, ...).
    /// The two call sites only contribute the database name, and they
    /// choose that themselves.
    /// </remarks>
    public static string BaseConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("MKN_TEST_ADMIN_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = "Host=127.0.0.1;Port=5432;Username=mkn_dev";
        }
        return raw;
    }

    /// <summary>
    /// Admin connection string: the base string, with Database set to
    /// <c>postgres</c>. Used to create and drop per-class databases and
    /// to open the scratch database in MigrationSchemaFixture.
    /// </summary>
    public static string AdminConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(BaseConnectionString())
        {
            Database = "postgres",
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Per-class connection string: the base string, with Database set
    /// to the supplied name. Used by TestDatabaseFixture for the
    /// per-class database it creates.
    /// </summary>
    public static string PerClassConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(BaseConnectionString())
        {
            Database = databaseName,
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Ensures the shared <c>mkn_test</c> database exists with the
    /// current migration chain applied.
    /// </summary>
    /// <remarks>
    /// The per-class fixture creates its own <c>mkn_test_{guid}</c>
    /// databases on demand. One class — <c>HttpSurfaceTests</c> — uses
    /// the shared <c>mkn_test</c> instead, because it holds a
    /// class-scoped <c>WebApplicationFactory&lt;Program&gt;</c> whose
    /// host is built once and reused across facts. On a developer
    /// machine <c>mkn_test</c> already exists (created manually). On a
    /// fresh CI runner it does not, and every HTTP request fails at
    /// <c>RelationalConnection.OpenAsync</c> with 3D000. This helper
    /// closes that gap by creating the database if absent and applying
    /// the migration chain.
    ///
    /// Idempotent. <c>CREATE DATABASE</c> is guarded on <c>pg_database</c>;
    /// <c>MigrateAsync</c> is itself idempotent (each migration is
    /// tracked by name in <c>__EFMigrationsHistory</c>).
    /// </remarks>
    public static async Task EnsureSharedTestDatabaseAsync()
    {
        const string databaseName = "mkn_test";

        await using (var admin = new NpgsqlConnection(AdminConnectionString()))
        {
            await admin.OpenAsync();
            await using var check = admin.CreateCommand();
            check.CommandText = "SELECT 1 FROM pg_database WHERE datname = @db";
            check.Parameters.AddWithValue("@db", databaseName);
            var exists = await check.ExecuteScalarAsync() is not null;

            if (!exists)
            {
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE {databaseName}";
                await create.ExecuteNonQueryAsync();
            }
        }

        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(PerClassConnectionString(databaseName))
            .Options;
        await using var db = new MknDbContext(options);
        await db.Database.MigrateAsync();
    }
}
