using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageProgramItem;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D9_EventProgram;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageProgramItemService.SoftDeleteAsync and the
/// ProgramItem cascade.
/// </summary>
public class ManageProgramItemServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(int ItemId, int AdminId);

    private async Task<Fixture> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "MC duty", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ProgramItem",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();


        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        var evt = new Event
        {
            Name = "Conf",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 2),
            Type = "Conference",
            HostBranchId = branch.BranchId,
            IsDeleted = false,
        };
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var program = new MknMs.Domain.D9_EventProgram.Program
        {
            EventId = evt.EventId,
            Title = "Main",
            IsDeleted = false,
        };
        Db.Programs.Add(program);
        await Db.SaveChangesAsync();

        var item = new ProgramItem
        {
            ProgramId = program.ProgramId,
            SequenceOrder = 1,
            Title = "Opening",
            ScheduledStart = Now.AddDays(15),
            ScheduledEnd = Now.AddDays(15).AddHours(1),
            IsDeleted = false,
        };
        Db.ProgramItems.Add(item);
        await Db.SaveChangesAsync();

        return new Fixture(item.ItemId, admin.AdminId);
    }

    private ManageProgramItemService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_RetainsItem()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId, true, "cancelled");

        result.IsSuccess.Should().BeTrue();



        var item = await Db.ProgramItems
            .AsNoTracking()
            .FirstAsync(i => i.ItemId == f.ItemId);
        item.IsDeleted.Should().BeTrue();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("No dependent changes apply");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId,
            consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "one");
        var second = await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "two");

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyInactive);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        // C6: the acting admin must resolve to an Admin row whose
        // IsActive = true.
        var f = await SeedAsync();
        var service = MakeService();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var program = await Db.Programs.FirstAsync();

        var result = await service.CreateAsync(
            program.ProgramId,
            1,
            "New item",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            serviceDefId: null,
            actingAdminId: f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    [Fact]
    public async Task ListForProgram_ExcludesSoftDeleted()
    {
        var f = await SeedAsync();
        var programId = await Db.ProgramItems
            .Where(i => i.ItemId == f.ItemId)
            .Select(i => i.ProgramId)
            .FirstAsync();

        var service = MakeService();
        await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "test");

        var items = await service.ListForProgramAsync(programId);
        items.Should().NotContain(i => i.ItemId == f.ItemId);
    }

    [Fact]
    public async Task Get_SoftDeleted_ReturnsNull()
    {
        var f = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "test");

        var item = await service.GetAsync(f.ItemId);
        item.Should().BeNull();
    }

    // -----------------------------------------------------------------
    // Matrix rows: 8.0 — ProgramItem-to-ServiceDefinition linkage
    // creates and preserves the event-sourced ServiceOccurrence.
    // -----------------------------------------------------------------

    private async Task<int> SeedServiceDefinitionAsync()
    {
        var serviceType = new ServiceType
        {
            Name = "Test ServiceType",
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceTypes.Add(serviceType);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Test Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        return definition.ServiceDefId;
    }

    // Matrix row 8.0:895806bf18d8: A ProgramItem with a ServiceDefID | An event-sourced ServiceOccurrence is created with `GeneratedBy = Administrator`
    [Fact]
    [Trait("MatrixRow", "8.0:895806bf18d8")]
    public async Task Create_WithServiceDef_CreatesEventSourcedOccurrence()
    {
        var f = await SeedAsync();
        var definitionId = await SeedServiceDefinitionAsync();
        var service = MakeService();

        var programId = await Db.ProgramItems
            .Where(i => i.ItemId == f.ItemId)
            .Select(i => i.ProgramId)
            .FirstAsync();

        var created = await service.CreateAsync(
            programId,
            sequenceOrder: 2,
            title: "Occurrence-creating item",
            scheduledStart: Now.AddDays(20),
            scheduledEnd: Now.AddDays(20).AddHours(1),
            serviceDefId: definitionId,
            actingAdminId: f.AdminId);

        created.IsSuccess.Should().BeTrue(
            $"creating a ProgramItem with a ServiceDefID must succeed; error: {created.Error?.Message}");

        Db.ChangeTracker.Clear();

        var occurrence = await Db.ServiceOccurrences.AsNoTracking()
            .FirstOrDefaultAsync(o => o.EventId != null
                && o.ScheduleId == null
                && o.GeneratedBy == "Administrator");

        occurrence.Should().NotBeNull(
            "the ProgramItem with ServiceDefID creates an event-sourced ServiceOccurrence");
        occurrence!.GeneratedBy.Should().Be("Administrator");
        occurrence.ScheduleId.Should().BeNull();
        occurrence.CreatedBy.Should().Be(f.AdminId);
    }

    // Matrix row 8.0:7ca957958322: A ProgramItem whose ServiceDefID is removed after an occurrence was created | The event-sourced ServiceOccurrence is not removed
    [Fact]
    [Trait("MatrixRow", "8.0:7ca957958322")]
    public async Task Update_ClearServiceDef_DoesNotRemoveExistingOccurrence()
    {
        var f = await SeedAsync();
        var definitionId = await SeedServiceDefinitionAsync();
        var service = MakeService();

        var programId = await Db.ProgramItems
            .Where(i => i.ItemId == f.ItemId)
            .Select(i => i.ProgramId)
            .FirstAsync();

        // Create with ServiceDef -> occurrence is created.
        var created = await service.CreateAsync(
            programId,
            sequenceOrder: 3,
            title: "Occurrence-creating item",
            scheduledStart: Now.AddDays(25),
            scheduledEnd: Now.AddDays(25).AddHours(1),
            serviceDefId: definitionId,
            actingAdminId: f.AdminId);
        created.IsSuccess.Should().BeTrue();
        var newItemId = created.Value!.ItemId;

        Db.ChangeTracker.Clear();

        var occurrencesBefore = await Db.ServiceOccurrences.AsNoTracking()
            .CountAsync(o => o.EventId != null
                && o.ScheduleId == null
                && o.GeneratedBy == "Administrator");
        occurrencesBefore.Should().Be(1);

        // Update the item with ServiceDefId = null.
        var updated = await service.UpdateAsync(
            newItemId,
            sequenceOrder: 3,
            title: "Occurrence-creating item (unlinked)",
            scheduledStart: Now.AddDays(25),
            scheduledEnd: Now.AddDays(25).AddHours(1),
            serviceDefId: null,
            actingAdminId: f.AdminId);
        updated.IsSuccess.Should().BeTrue(
            $"clearing the ServiceDefID must succeed; error: {updated.Error?.Message}");

        Db.ChangeTracker.Clear();

        // The occurrence is NOT removed. §9.1.11: cleanup of a
        // removed ServiceDefID is an administrator action, not a system
        // behaviour. The occurrence survives the update.
        var occurrencesAfter = await Db.ServiceOccurrences.AsNoTracking()
            .CountAsync(o => o.EventId != null
                && o.ScheduleId == null
                && o.GeneratedBy == "Administrator");
        occurrencesAfter.Should().Be(1,
            "removing the ServiceDefID does not remove the event-sourced occurrence");
    }
    // Regression for D1: ProgramItem.ScheduledStart / ScheduledEnd are
    // DateTimeOffset on 'timestamp with time zone'. Npgsql rejects a
    // non-zero offset. The service must normalise to UTC on write so
    // callers can supply a local-time offset and the row persists.
    [Fact]
    public async Task Create_WithNonUtcOffset_StoresUtcInstant()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var programId = await Db.Programs
            .Select(p => p.ProgramId)
            .FirstAsync();

        // 2026-11-01T09:00:00+02:00 == 2026-11-01T07:00:00Z
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(2));
        var end = new DateTimeOffset(2026, 11, 1, 10, 30, 0, TimeSpan.FromHours(2));

        var result = await service.CreateAsync(
            programId,
            sequenceOrder: 99,
            title: "UTC-offset item",
            scheduledStart: start,
            scheduledEnd: end,
            serviceDefId: null,
            actingAdminId: f.AdminId);

        result.IsSuccess.Should().BeTrue($"error: {result.Error?.Message}");

        Db.ChangeTracker.Clear();
        var persisted = await Db.ProgramItems
            .AsNoTracking()
            .FirstAsync(i => i.Title == "UTC-offset item");

        persisted.ScheduledStart.Offset.Should().Be(TimeSpan.Zero,
            "DateTimeOffset columns store instants; the write must be UTC");
        persisted.ScheduledStart.UtcDateTime.Should().Be(
            new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc));
        persisted.ScheduledEnd.UtcDateTime.Should().Be(
            new DateTime(2026, 11, 1, 8, 30, 0, DateTimeKind.Utc));
    }

    // Regression for F2: StartTime on an event-sourced occurrence must
    // be the caller's wall-clock time, not the UTC wall-clock derived
    // from the normalised instant. §C7.
    [Fact]
    public async Task Create_WithNonUtcOffset_SetsOccurrenceStartTimeFromCallerWallClock()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var programId = await Db.Programs
            .Select(p => p.ProgramId)
            .FirstAsync();

        // SeedAsync does not create a ServiceDefinition; seed one with
        // its referenced ServiceType so the ProgramItem->occurrence
        // derivation can resolve the definition.
        var serviceType = new ServiceType
        {
            Name = "WallClockTestType", IsActive = true, IsDeleted = false,
        };
        Db.ServiceTypes.Add(serviceType);
        await Db.SaveChangesAsync();

        var serviceDefinition = new ServiceDefinition
        {
            Name = "WallClockTestService",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
            IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(serviceDefinition);
        await Db.SaveChangesAsync();

        var serviceDefId = serviceDefinition.ServiceDefId;

        // 2026-11-01T09:00:00+02:00 is 2026-11-01T07:00:00Z.
        // The occurrence's StartTime must read 09:00 — the caller's
        // wall-clock — not 07:00 — the UTC wall-clock.
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(2));
        var end = new DateTimeOffset(2026, 11, 1, 10, 30, 0, TimeSpan.FromHours(2));

        var result = await service.CreateAsync(
            programId,
            sequenceOrder: 100,
            title: "Wall-clock-start item",
            scheduledStart: start,
            scheduledEnd: end,
            serviceDefId: serviceDefId,
            actingAdminId: f.AdminId);

        result.IsSuccess.Should().BeTrue($"error: {result.Error?.Message}");

        Db.ChangeTracker.Clear();

        // The stored ProgramItem is a UTC instant (D1's contract).
        var item = await Db.ProgramItems.AsNoTracking()
            .FirstAsync(i => i.Title == "Wall-clock-start item");
        item.ScheduledStart.Offset.Should().Be(TimeSpan.Zero);
        item.ScheduledStart.UtcDateTime.Should().Be(
            new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc));

        // The event-sourced occurrence carries the caller's wall-clock.
        var occurrence = await Db.ServiceOccurrences.AsNoTracking()
            .Where(o => o.EventId != null && o.ScheduleId == null
                && o.Date == new DateOnly(2026, 11, 1))
            .OrderByDescending(o => o.OccurrenceId)
            .FirstAsync();
        occurrence.StartTime.Should().Be(new TimeOnly(9, 0),
            "§C7: StartTime is a wall-clock time, not a UTC-derived one");
        occurrence.GeneratedBy.Should().Be("Administrator");
    }

}
