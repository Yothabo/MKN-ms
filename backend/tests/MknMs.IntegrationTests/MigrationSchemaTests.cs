using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Persistence;
using Npgsql;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// F-08 clean-database schema verification.
/// </summary>
/// <remarks>
/// Applies the full migration chain to a scratch database, then
/// inspects the resulting PostgreSQL catalog. The scratch database is
/// created in the fixture and dropped in the fixture's DisposeAsync.
/// It never touches mkn_test or mkn_dev.
///
/// The facts assert the shapes §4 defines — EventDuty's column set,
/// the check-constraint family the migrations add, and the unique
/// indexes §15.2 declares. They do not enumerate every column of
/// every entity; that belongs to the schema document and to review,
/// not to this test.
///
/// Source: §4, §15.2.1 through §15.2.4, migration
/// 20261011071139_AddStep7Constraints, migration
/// 20261016082951_RestoreEventDutyToSpec4Shape. Audit item 16.1.
/// </remarks>
public class MigrationSchemaTests : IClassFixture<MigrationSchemaFixture>
{
    private readonly MigrationSchemaFixture _fixture;

    public MigrationSchemaTests(MigrationSchemaFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EventDuty_HasSpec4Columns()
    {
        var expected = new[]
        {
            "event_duty_id", "event_id", "duty_id", "service_def_id",
            "required_slot_count", "is_active", "is_deleted",
        };

        var actual = await _fixture.ColumnsAsync("event_duty");
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task EventDuty_LegacyColumnsAbsent()
    {
        var legacy = new[]
        {
            "program_item_id", "assigned_member_id", "assignment_status_id", "label",
        };

        var actual = await _fixture.ColumnsAsync("event_duty");
        actual.Should().NotContain(legacy);
    }

    [Fact]
    public async Task EventDuty_HasFlagConstraint()
    {
        var constraints = await _fixture.CheckConstraintsAsync("event_duty");
        constraints.Should().Contain("chk_event_duty_flags");
    }

    [Fact]
    public async Task FlagConstraints_PresentOnEveryTwoFlagTable()
    {
        var expectedTables = new[]
        {
            "role", "duty", "duty_rule", "branch", "branch_time_slot",
            "service_definition", "service_definition_duty", "service_schedule",
            "event", "event_duty", "member_status", "attribute_type",
            "capability", "attendance_rule", "notification_subscription",
            "entity_deletion_policy", "outcome_state", "assignment_status",
            "permission_tier", "service_type", "time_of_day",
        };

        foreach (var table in expectedTables)
        {
            var constraints = await _fixture.CheckConstraintsAsync(table);
            constraints.Should().Contain(
                $"chk_{table}_flags",
                because: $"table {table} carries both IsActive and IsDeleted");
        }
    }

    [Fact]
    public async Task ServiceOccurrence_HasSourceConstraint()
    {
        var constraints = await _fixture.CheckConstraintsAsync("service_occurrence");
        constraints.Should().Contain("chk_service_occurrence_source");
    }

    [Fact]
    public async Task RosterAssignment_HasUniqueMemberDutyOccurrence()
    {
        var indexes = await _fixture.UniqueIndexesAsync("roster_assignment");
        indexes.Should().Contain("uq_roster_assignment_member_duty_occurrence");
    }

    [Fact]
    public async Task AttendanceRecord_HasUniqueMemberOccurrence()
    {
        var indexes = await _fixture.UniqueIndexesAsync("attendance_record");
        indexes.Should().Contain("uq_attendance_record_member_occurrence");
    }

    [Fact]
    public async Task ServiceSchedule_HasUniqueActiveIndex()
    {
        var indexes = await _fixture.UniqueIndexesAsync("service_schedule");
        indexes.Should().Contain("uq_service_schedule_active");

        var filter = await _fixture.IndexFilterAsync(
            "service_schedule", "uq_service_schedule_active");
        filter.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ServiceOccurrence_HasUniqueScheduleDateIndex()
    {
        var indexes = await _fixture.UniqueIndexesAsync("service_occurrence");
        indexes.Should().Contain("uq_service_occurrence_schedule_date");

        var filter = await _fixture.IndexFilterAsync(
            "service_occurrence", "uq_service_occurrence_schedule_date");
        filter.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Program_HasUniqueEventIndex()
    {
        var indexes = await _fixture.UniqueIndexesAsync("program");
        indexes.Should().Contain("uq_program_event");
    }

    [Fact]
    public async Task MemberAttributeValue_HasUniqueMemberType()
    {
        var indexes = await _fixture.UniqueIndexesAsync("member_attribute_value");
        indexes.Should().Contain("uq_member_attribute_value_member_type");
    }
}

/// <summary>
/// Creates and drops the scratch database; migrates it once; exposes
/// catalog read helpers for the tests.
/// </summary>
public sealed class MigrationSchemaFixture : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=127.0.0.1;Port=5432;Database=postgres;Username=mkn_dev";

    private const string ScratchPrefix = "mkn_migration_";

    private string _scratchDb = null!;
    private string _scratchConnectionString = null!;
    private MknDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _scratchDb = ScratchPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE {_scratchDb}";
            await cmd.ExecuteNonQueryAsync();
        }

        _scratchConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={_scratchDb};Username=mkn_dev";

        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(_scratchConnectionString)
            .Options;
        _db = new MknDbContext(options);

        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        // Npgsql pools physical connections by default. After the EF
        // context is disposed, the underlying connection returns to
        // the pool still open against the scratch database, and
        // DROP DATABASE will fail against it. Clear the pool first;
        // the subsequent pg_terminate_backend then finds nothing on
        // the scratch database and DROP succeeds.
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();

        await using (var terminate = admin.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity "
                + "WHERE datname = @db AND pid <> pg_backend_pid()";
            terminate.Parameters.AddWithValue("@db", _scratchDb);
            await terminate.ExecuteNonQueryAsync();
        }

        await using (var drop = admin.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE IF EXISTS {_scratchDb}";
            await drop.ExecuteNonQueryAsync();
        }
    }

    public async Task<IReadOnlyList<string>> ColumnsAsync(string table)
    {
        var result = new List<string>();
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT column_name FROM information_schema.columns "
            + "WHERE table_schema = 'public' AND table_name = @t "
            + "ORDER BY column_name";
        AddParam(cmd, "@t", table);
        await _db.Database.OpenConnectionAsync();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task<IReadOnlyList<string>> CheckConstraintsAsync(string table)
    {
        var result = new List<string>();
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT conname FROM pg_constraint c "
            + "JOIN pg_class t ON t.oid = c.conrelid "
            + "JOIN pg_namespace n ON n.oid = t.relnamespace "
            + "WHERE n.nspname = 'public' AND t.relname = @t "
            + "AND c.contype = 'c'";
        AddParam(cmd, "@t", table);
        await _db.Database.OpenConnectionAsync();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task<IReadOnlyList<string>> UniqueIndexesAsync(string table)
    {
        var result = new List<string>();
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT c.relname FROM pg_index i "
            + "JOIN pg_class c ON c.oid = i.indexrelid "
            + "JOIN pg_class t ON t.oid = i.indrelid "
            + "JOIN pg_namespace n ON n.oid = t.relnamespace "
            + "WHERE n.nspname = 'public' AND t.relname = @t AND i.indisunique";
        AddParam(cmd, "@t", table);
        await _db.Database.OpenConnectionAsync();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    public async Task<string?> IndexFilterAsync(string table, string indexName)
    {
        await using var cmd = _db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText =
            "SELECT pg_get_expr(i.indpred, i.indrelid) FROM pg_index i "
            + "JOIN pg_class c ON c.oid = i.indexrelid "
            + "JOIN pg_class t ON t.oid = i.indrelid "
            + "JOIN pg_namespace n ON n.oid = t.relnamespace "
            + "WHERE n.nspname = 'public' AND t.relname = @t AND c.relname = @i";
        AddParam(cmd, "@t", table);
        AddParam(cmd, "@i", indexName);
        await _db.Database.OpenConnectionAsync();
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
