
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
}
