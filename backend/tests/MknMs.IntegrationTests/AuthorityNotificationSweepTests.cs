using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for AuthorityNotificationSweepService (9.0 Path 2).
/// </summary>
/// <remarks>
/// The sweep's per-recipient dispatch-failure branch (acceptance matrix
/// row 8) is not tested. The operation inside the try is logging; the
/// only other failure source, the channel setting, is caught before row
/// processing. A real transport introduces the failure path. Adding a
/// seam now would create a production abstraction with no current
/// functional need. Revisit when the transport is real.
/// </remarks>
public class AuthorityNotificationSweepTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(int AdminIdA, int AdminIdB, int TierId);

    private async Task<Fixture> SeedAsync(string? channel = "Log")
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);

        if (channel is not null)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "NotificationChannel",
                Value = channel,
                Required = true,
            });
        }

        await Db.SaveChangesAsync();

        var memberA = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        var memberB = new Member
        {
            Name = "B", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(memberA);
        Db.Members.Add(memberB);
        await Db.SaveChangesAsync();

        var adminA = new Admin
        {
            MemberId = memberA.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        var adminB = new Admin
        {
            MemberId = memberB.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        Db.Admins.Add(adminA);
        Db.Admins.Add(adminB);
        await Db.SaveChangesAsync();

        return new Fixture(adminA.AdminId, adminB.AdminId, tier.PermissionTierId);
    }

    private async Task<int> SeedAuditAsync(string action = "Deactivate")
    {
        var row = new ConfigurationAuditLog
        {
            EntityType = "Role",
            EntityId = 1,
            EntityName = "Coordinator",
            Action = action,
            Reason = "test",
            InitiatedByAdminId = 1,
            ApprovedByAdminId = 1,
            InitiatedAt = Now,
            ApprovedAt = Now,
            ConsequencesPreviewed = "preview",
            ConsequencesOccurred = "occurred",
            NotifiedAt = null,
        };
        Db.ConfigurationAuditLogs.Add(row);
        await Db.SaveChangesAsync();
        return row.AuditId;
    }

    private AuthorityNotificationSweepService MakeService(
        ILogger<AuthorityNotificationSweepService>? logger = null) =>
        new(Db, _clock, logger ?? new CapturingLogger<AuthorityNotificationSweepService>());

    [Fact]
    public async Task WithNoPendingRows_Succeeds()
    {
        await SeedAsync();
        var service = MakeService();

        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.AuditRowsProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ZeroRecipients_ProcessesRowAndSetsNotifiedAt()
    {
        await SeedAsync();
        var auditId = await SeedAuditAsync();
        var service = MakeService();

        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.AuditRowsProcessed.Should().Be(1);
        result.NotificationsDispatched.Should().Be(0);

        var row = await Db.ConfigurationAuditLogs
            .AsNoTracking()
            .FirstAsync(a => a.AuditId == auditId);
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubscriptionByTier_DispatchesToAllActiveAdmins()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.NotificationsDispatched.Should().Be(2);

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubscriptionByAdmin_DispatchesToThatAdminOnly()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientAdminId = f.AdminIdA,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(1);
    }

    [Fact]
    public async Task InactiveSubscription_IsNotConsulted()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
        result.AuditRowsProcessed.Should().Be(1);
    }

    [Fact]
    public async Task SubscriptionByEventTypeMismatch_DoesNotDispatch_SetsNotifiedAt()
    {
        var f = await SeedAsync();
        var auditId = await SeedAuditAsync(action: "Deactivate");

        // The subscription names SoftDelete; the row is a Deactivate.
        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "SoftDelete",
            RecipientTierId = f.TierId,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        // Zero recipients is a valid outcome. The row is processed and
        // NotifiedAt is set, per B16.
        result.NotificationsDispatched.Should().Be(0);
        result.AuditRowsProcessed.Should().Be(1);

        var row = await Db.ConfigurationAuditLogs
            .AsNoTracking()
            .FirstAsync(a => a.AuditId == auditId);
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MissingChannel_FailsWithoutProcessing()
    {
        await SeedAsync(channel: null);
        await SeedAuditAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_missing");

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
        row.NotifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnrecognisedChannel_FailsWithoutProcessing()
    {
        await SeedAsync(channel: "Email");
        await SeedAuditAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_invalid");
    }

    [Fact]
    public async Task SoftDeletedSubscription_IsNotConsulted()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = false,
            IsDeleted = true,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
    }

    [Fact]
    public async Task SoftDeletedAdmin_IsNotResolvedAsRecipient()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminIdA);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientAdminId = f.AdminIdA,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
        result.AuditRowsProcessed.Should().Be(1);
    }

    [Fact]
    public async Task Sweep_SkipsRowWithNotifiedAtSet()
    {
        await SeedAsync();
        var auditId = await SeedAuditAsync();

        var row = await Db.ConfigurationAuditLogs
            .FirstAsync(a => a.AuditId == auditId);
        var alreadyNotified = Now.AddMinutes(-30);
        row.NotifiedAt = alreadyNotified;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.AuditRowsProcessed.Should().Be(0);
        result.NotificationsDispatched.Should().Be(0);

        var after = await Db.ConfigurationAuditLogs
            .AsNoTracking()
            .FirstAsync(a => a.AuditId == auditId);
        after.NotifiedAt.Should().Be(alreadyNotified);
    }

    [Fact]
    public async Task TwoSubscriptionsMatchSameAuditRow_BothDispatch()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        // One by-tier subscription resolves both admins. One by-admin
        // subscription resolves AdminIdA. The union is both admins;
        // NotificationsDispatched counts per resolved recipient, so
        // the total is 2.
        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = true,
            IsDeleted = false,
        });
        Db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientAdminId = f.AdminIdA,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.NotificationsDispatched.Should().Be(2);

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ZeroRecipients_LogsWarning()
    {
        // Zero recipients is not a failure (B16) — the row is marked
        // notified — but it is worth surfacing. Operators must be able
        // to see that a configuration event dispatched to no one.
        await SeedAsync();
        var auditId = await SeedAuditAsync();

        var logger = new CapturingLogger<AuthorityNotificationSweepService>();
        var service = MakeService(logger);

        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.AuditRowsProcessed.Should().Be(1);
        result.NotificationsDispatched.Should().Be(0);

        logger.Entries.Should().Contain(
            e => e.Level == LogLevel.Warning
                && e.Message.Contains("zero recipients")
                && e.Message.Contains($"audit {auditId}"));

        var row = await Db.ConfigurationAuditLogs
            .AsNoTracking()
            .FirstAsync(a => a.AuditId == auditId);
        row.NotifiedAt.Should().NotBeNull();
    }
}
