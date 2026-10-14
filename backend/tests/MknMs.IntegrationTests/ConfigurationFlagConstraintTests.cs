using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Cross-cutting configuration invariant. Specification §9.7:
/// <c>IsActive = true</c> and <c>IsDeleted = true</c> is a forbidden
/// combination on every configuration entity that carries both flags.
/// </summary>
/// <remarks>
/// The forbidden combination is enforced by a database check
/// constraint, added by migration
/// 20261011071139_AddStep7Constraints, named
/// <c>chk_&lt;table&gt;_flags</c>. For the Role table the constraint
/// is <c>chk_role_flags</c>, defined as
/// <c>CHECK (NOT (is_active = TRUE AND is_deleted = TRUE))</c>.
///
/// The test below proves the database constraint is enforced. It is
/// not a test of application validation: the application never sets
/// the forbidden combination. The test writes the combination
/// directly to the entity and attempts to persist it, expecting the
/// database to reject the write.
///
/// Role is one of the twenty-one tables carrying the constraint. The
/// test is representative; the constraint's presence on the other
/// twenty tables is asserted by the migration and is not exercised
/// here.
/// </remarks>
public class ConfigurationFlagConstraintTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE role RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Role_BothFlagsTrue_DatabaseRejects()
    {
        var role = new Role
        {
            Name = "Constraint probe",
            IsActive = true,
            IsDeleted = false,
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        // Both flags true: the forbidden combination. §9.7 forbids it.
        role.IsActive = true;
        role.IsDeleted = true;

        var act = async () => await _db.SaveChangesAsync();

        var exception = await act.Should()
            .ThrowAsync<DbUpdateException>();

        // After a failed SaveChangesAsync the change tracker is in a
        // state that cannot be reused. Dispose it before any further
        // database work.
        _db.ChangeTracker.Clear();

        // The row in the database still carries the valid combination.
        await using var fresh = new MknDbContext(
            new DbContextOptionsBuilder<MknDbContext>()
                .UseNpgsql(TestConnectionString)
                .Options);

        var persisted = await fresh.Roles
            .AsNoTracking()
            .FirstAsync(r => r.RoleId == role.RoleId);

        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();
    }
}
