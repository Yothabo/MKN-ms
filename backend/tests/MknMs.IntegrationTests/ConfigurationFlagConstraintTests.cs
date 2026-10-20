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
public class ConfigurationFlagConstraintTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // Three facts each insert their own IsDefault = true row into
        // role / member_status / permission_tier, and one fact requires
        // no default row at all. The per-class database persists across
        // facts, so clear the three affected tables before each fact.
        Db.Roles.RemoveRange(Db.Roles);
        Db.MemberStatuses.RemoveRange(Db.MemberStatuses);
        Db.PermissionTiers.RemoveRange(Db.PermissionTiers);
        await Db.SaveChangesAsync();
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
        Db.Roles.Add(role);
        await Db.SaveChangesAsync();

        // Both flags true: the forbidden combination. §9.7 forbids it.
        role.IsActive = true;
        role.IsDeleted = true;

        var act = async () => await Db.SaveChangesAsync();

        var exception = await act.Should()
            .ThrowAsync<DbUpdateException>();

        // After a failed SaveChangesAsync the change tracker is in a
        // state that cannot be reused. Dispose it before any further
        // database work.
        Db.ChangeTracker.Clear();

        // The row in the database still carries the valid combination.
        await using var fresh = CreateContext();

        var persisted = await fresh.Roles
            .AsNoTracking()
            .FirstAsync(r => r.RoleId == role.RoleId);

        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();
    }
    [Fact]
    public async Task Role_SecondDefault_DatabaseRejects()
    {
        var first = new MknMs.Domain.D1_RoleDuty.Role
        {
            Name = "DefaultRole", IsDefault = true, IsActive = true, IsDeleted = false,
        };
        Db.Roles.Add(first);
        await Db.SaveChangesAsync();

        var second = new MknMs.Domain.D1_RoleDuty.Role
        {
            Name = "SecondDefault", IsDefault = true, IsActive = true, IsDeleted = false,
        };
        Db.Roles.Add(second);
        var act = async () => await Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task MemberStatus_SecondDefault_DatabaseRejects()
    {
        var first = new MknMs.Domain.D4_Member.MemberStatus
        {
            Name = "DefaultStatus", IsRosterable = true, IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        Db.MemberStatuses.Add(first);
        await Db.SaveChangesAsync();

        var second = new MknMs.Domain.D4_Member.MemberStatus
        {
            Name = "SecondDefault", IsRosterable = true, IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        Db.MemberStatuses.Add(second);
        var act = async () => await Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task PermissionTier_SecondDefault_DatabaseRejects()
    {
        var first = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "DefaultTier", IsDefault = true, IsActive = true, IsDeleted = false,
        };
        Db.PermissionTiers.Add(first);
        await Db.SaveChangesAsync();

        var second = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "SecondDefault", IsDefault = true, IsActive = true, IsDeleted = false,
        };
        Db.PermissionTiers.Add(second);
        var act = async () => await Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
