using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureBranch;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureBranchService.SoftDeleteAsync and the Branch
/// cascade.
/// </summary>
public class ConfigureBranchServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 16, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int branchId, int adminId)> SeedAsync(bool withMember = false)
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var otherBranch = new Branch { Name = "B2", Location = "L2", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var timeOfDay = new TimeOfDay { Name = "Morning" };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.Branches.Add(otherBranch);
        Db.PermissionTiers.Add(tier);
        Db.TimeOfDays.Add(timeOfDay);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Branch",
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
            BranchId = otherBranch.BranchId, RoleId = role.RoleId,
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
        await Db.SaveChangesAsync();

        if (withMember)
        {
            var extra = new Member
            {
                Name = "Extra", Surname = "User",
                JoinDate = new DateOnly(2024, 1, 1),
                DateOfBirth = new DateOnly(1990, 1, 1),
                MembershipStage = "Full", Gender = "Other",
                Phone = "+27-2", Email = null,
                BranchId = branch.BranchId, RoleId = role.RoleId,
                MemberStatusId = statusId,
            };
            Db.Members.Add(extra);
            await Db.SaveChangesAsync();
        }

        // Branch time slots referencing the branch.
        Db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        Db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Wednesday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        return (branch.BranchId, admin.AdminId);
    }

    private ConfigureBranchService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    // Matrix row 1.0: A Branch with a Member assigned to it | A soft delete is requested
    [Fact]
    [Trait("MatrixRow", "1.0:286671fb9e11")]
    public async Task SoftDelete_WithMembers_IsBlocked()
    {
        var (branch, admin) = await SeedAsync(withMember: true);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeFalse();

        var slots = await Db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
        slots.Should().Be(2, "a blocked soft delete must not remove slots");
    }

    // Matrix row 1.0: A Branch with no Member assigned to it | A soft delete is requested
    [Fact]
    [Trait("MatrixRow", "1.0:88abaee2a361")]
    [Trait("MatrixRow", "Deletion:59e1838037b4")]
    public async Task SoftDelete_NoMembers_RemovesTimeSlots()
    {
        var (branch, admin) = await SeedAsync(withMember: false);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "not used");

        result.IsSuccess.Should().BeTrue();

        var slots = await Db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
        slots.Should().Be(0);

        var persisted = await Db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeTrue();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (branch, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (branch, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var (_, actingAdminId) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("NewBranch", "Somewhere", actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    [Fact]
    public async Task List_ExcludesSoftDeleted()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branches = await service.ListAsync();
        branches.Should().NotContain(b => b.BranchId == branchId);
    }

    [Fact]
    public async Task List_IncludeInactive_ExcludesSoftDeleted()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branches = await service.ListAsync(includeInactive: true);
        branches.Should().NotContain(b => b.BranchId == branchId);
    }

    [Fact]
    public async Task Get_SoftDeleted_ReturnsNull()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branch = await service.GetAsync(branchId);
        branch.Should().BeNull();
    }

    [Fact]
    public async Task Reactivate_DeactivatedBranch_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            branchId, adminId, adminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var result = await service.ReactivateAsync(branchId, adminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.Branches
            .AsNoTracking()
            .FirstAsync(b => b.BranchId == branchId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        // §9.7's audit scope is deactivation and soft delete; reactivation
        // writes no row.
        var auditCount = await Db.ConfigurationAuditLogs.CountAsync();
        auditCount.Should().Be(1);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedBranch_Fails()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var result = await service.ReactivateAsync(branchId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.Branches
            .AsNoTracking()
            .FirstAsync(b => b.BranchId == branchId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveBranch_Fails()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateAsync(branchId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }

    // -----------------------------------------------------------------
    // Matrix row Configuration (consolidated): a configuration change does
    // not rewrite any operational record. Trait identity TBD — computed
    // by matrix_tool.py after this fact lands.
    // -----------------------------------------------------------------

    [Fact]
    [Trait("MatrixRow", "Configuration:3e943ba952e6")]
    public async Task SoftDelete_OperationalRecordsUnaffected()
    {
        var (branchId, adminId) = await SeedAsync(withMember: false);

        // The branch being soft-deleted has an empty roster. A second
        // branch hosts a full schedule + occurrence + assignment. The
        // cascade on the deleted branch must not touch the assignment.
        var otherBranch = await Db.Branches
            .Where(b => b.Name == "B2")
            .FirstAsync();

        var timeOfDay = await Db.TimeOfDays.FirstAsync();
        var otherSlot = new MknMs.Domain.D3_BranchTimeSlotService.BranchTimeSlot
        {
            BranchId = otherBranch.BranchId,
            DayOfWeek = "Saturday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsActive = true, IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(otherSlot);
        await Db.SaveChangesAsync();

        var serviceType = new MknMs.Domain.D10_ConfigLookups.ServiceType
        {
            Name = "ConfigTestServiceType", IsActive = true, IsDeleted = false,
        };
        Db.ServiceTypes.Add(serviceType);
        await Db.SaveChangesAsync();

        var definition = new MknMs.Domain.D3_BranchTimeSlotService.ServiceDefinition
        {
            Name = "ConfigTestService",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        var schedule = new MknMs.Domain.D3_BranchTimeSlotService.ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = otherSlot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        var occurrence = new MknMs.Domain.D3_BranchTimeSlotService.ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 12, 1),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        var duty = new MknMs.Domain.D1_RoleDuty.Duty
        {
            Name = "ConfigTestDuty", IsActive = true, IsDeleted = false,
        };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        var member = await Db.Members
            .Where(m => m.Name == "Admin")
            .FirstAsync();

        var assignment = new MknMs.Domain.D7_RosterAssignment.RosterAssignment
        {
            MemberId = member.MemberId,
            DutyId = duty.DutyId,
            OccurrenceId = occurrence.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = Now,
        };
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        var assignmentSnapshot = await Db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == assignment.AssignmentId);

        // Soft-delete the empty branch. Its two time slots are removed;
        // the roster assignment (at the other branch's occurrence) must
        // be untouched.
        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            branchId, adminId, adminId, true, "test cleanup");
        result.IsSuccess.Should().BeTrue(
            $"branch soft delete must succeed; error: {result.Error?.Message}");

        Db.ChangeTracker.Clear();

        var assignmentAfter = await Db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == assignment.AssignmentId);

        assignmentAfter.MemberId.Should().Be(assignmentSnapshot.MemberId);
        assignmentAfter.DutyId.Should().Be(assignmentSnapshot.DutyId);
        assignmentAfter.OccurrenceId.Should().Be(assignmentSnapshot.OccurrenceId);
        assignmentAfter.AssignmentStatusId.Should().Be(assignmentSnapshot.AssignmentStatusId);
        assignmentAfter.AssignmentSource.Should().Be(assignmentSnapshot.AssignmentSource);
        assignmentAfter.CreatedAt.Should().Be(assignmentSnapshot.CreatedAt);
    }
}
