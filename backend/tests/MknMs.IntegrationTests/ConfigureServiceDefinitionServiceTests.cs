using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureServiceDefinition;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureServiceDefinitionService.SoftDeleteAsync.
/// </summary>
public class ConfigureServiceDefinitionServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int defId, int adminId, int stId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var st = new ServiceType { Name = "Regular" };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.ServiceTypes.Add(st);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceDefinition",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();


        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(adminMember);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = adminMember.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);

        var def = new ServiceDefinition
        {
            Name = "Sunday", ServiceTypeId = st.ServiceTypeId, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(def);
        await Db.SaveChangesAsync();

        return (def.ServiceDefId, admin.AdminId, st.ServiceTypeId);
    }

    private ConfigureServiceDefinitionService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    // Matrix row 1.0: A Service Definition with no dependent rows | A soft delete is requested
    [Fact]
    [Trait("MatrixRow", "1.0:3232070cacb6")]
    public async Task SoftDelete_Unreferenced_Succeeds()
    {
        var (def, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "unused");

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == def);
        persisted.IsDeleted.Should().BeTrue();
    }

    // Matrix row 1.0: A Service Definition referenced by a Service Schedule, Program Item, Event Duty, or Duty Rule row | A soft delete is requested
    [Fact]
    [Trait("MatrixRow", "1.0:78512b655bb1")]
    public async Task SoftDelete_ReferencedBySchedule_IsBlocked()
    {
        var (def, admin, _) = await SeedAsync();

        var tod = new TimeOfDay { Name = "Morning" };
        Db.TimeOfDays.Add(tod);
        await Db.SaveChangesAsync();

        var branch = await Db.Branches.FirstAsync();
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        Db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == def);
        persisted.IsDeleted.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.CountAsync();
        audit.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_ReferencedByDutyRule_IsBlocked()
    {
        var (def, admin, _) = await SeedAsync();

        var duty = new Duty { Name = "Reading", IsDeleted = false };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = duty.DutyId,
            ServiceDefId = def,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "R",
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (def, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            def, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
    [Fact]
    public async Task Deactivate_WithInactiveAdmin_Fails()
    {
        var (defId, actingAdminId, _) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.DeactivateAsync(
            defId, actingAdminId, actingAdminId, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    // Matrix row 1.0:e614859b3705: A ServiceDefinition whose OwningBranchID is changed to a branch at which existing schedules' time slots do not reside | The update is rejected
    [Fact]
    [Trait("MatrixRow", "1.0:e614859b3705")]
    public async Task Update_OwningBranchChange_WithMismatchedSchedule_Fails()
    {
        var (defId, actingAdminId, stId) = await SeedAsync();

        // Create a branch different from the current owning branch and
        // attach a schedule at a time slot on it.
        var otherBranch = new Branch { Name = "Other", Location = "Elsewhere" };
        Db.Branches.Add(otherBranch);
        var tod = new TimeOfDay { Name = "Morning" };
        Db.TimeOfDays.Add(tod);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = otherBranch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        Db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = defId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        });
        await Db.SaveChangesAsync();

        // Pick a different branch as the new owning branch.
        var newOwningBranch = new Branch { Name = "Third", Location = "Third" };
        Db.Branches.Add(newOwningBranch);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.UpdateAsync(
            defId, "Updated", stId, newOwningBranch.BranchId, actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    // Matrix row 1.0:e614859b3705: A ServiceDefinition whose OwningBranchID is changed to a branch at which existing schedules' time slots do not reside | The update is rejected
    [Fact]
    [Trait("MatrixRow", "1.0:e614859b3705")]
    public async Task Update_OwningBranchChange_NoSchedules_Succeeds()
    {
        var (defId, actingAdminId, stId) = await SeedAsync();

        var newOwningBranch = new Branch { Name = "Other", Location = "Elsewhere" };
        Db.Branches.Add(newOwningBranch);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.UpdateAsync(
            defId, "Updated", stId, newOwningBranch.BranchId, actingAdminId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_DeactivatedServiceDefinition_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var (defId, actingAdminId, _) = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            defId, actingAdminId, actingAdminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateAsync(defId, actingAdminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == defId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedServiceDefinition_Fails()
    {
        var (defId, actingAdminId, _) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            defId, actingAdminId, actingAdminId, true, "test");

        var result = await service.ReactivateAsync(defId, actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == defId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveServiceDefinition_Fails()
    {
        var (defId, actingAdminId, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateAsync(defId, actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
