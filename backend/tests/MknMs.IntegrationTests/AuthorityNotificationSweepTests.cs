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
public class AuthorityNotificationSweepTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private MknDbContext _db = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _clock = new FakeTimeProvider(Now);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE configuration_audit_log, entity_deletion_policy, " +
            "notification_subscription, admin, member, member_status, branch, " +
            "role, permission_tier, system_setting " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Fixture(int AdminIdA, int AdminIdB, int TierId);

    private async Task<Fixture> SeedAsync(string? channel = "Log")
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);

        if (channel is not null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "NotificationChannel",
                Value = channel,
                Required = true,
            });
        }

        await _db.SaveChangesAsync();

        var memberA = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
        };
        var memberB = new Member
        {
            Name = "B", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
        };
        _db.Members.Add(memberA);
        _db.Members.Add(memberB);
        await _db.SaveChangesAsync();

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
        _db.Admins.Add(adminA);
        _db.Admins.Add(adminB);
        await _db.SaveChangesAsync();

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
        _db.ConfigurationAuditLogs.Add(row);
        await _db.SaveChangesAsync();
        return row.AuditId;
    }

    private AuthorityNotificationSweepService MakeService() =>
        new(_db, _clock, new CapturingLogger<AuthorityNotificationSweepService>());

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

        var row = await _db.ConfigurationAuditLogs
            .AsNoTracking()
            .FirstAsync(a => a.AuditId == auditId);
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubscriptionByTier_DispatchesToAllActiveAdmins()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.Status.Should().Be("Success");
        result.NotificationsDispatched.Should().Be(2);

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
        row.NotifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubscriptionByAdmin_DispatchesToThatAdminOnly()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientAdminId = f.AdminIdA,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(1);
    }

    [Fact]
    public async Task InactiveSubscription_IsNotConsulted()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
        result.AuditRowsProcessed.Should().Be(1);
    }

    [Fact]
    public async Task SubscriptionByEventTypeMismatch_DoesNotDispatch()
    {
        var f = await SeedAsync();
        await SeedAuditAsync(action: "Deactivate");

        // The subscription names SoftDelete; the row is a Deactivate.
        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "SoftDelete",
            RecipientTierId = f.TierId,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
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

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
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

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientTierId = f.TierId,
            IsActive = false,
            IsDeleted = true,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
    }

    [Fact]
    public async Task SoftDeletedAdmin_IsNotResolvedAsRecipient()
    {
        var f = await SeedAsync();
        await SeedAuditAsync();

        var admin = await _db.Admins.FirstAsync(a => a.AdminId == f.AdminIdA);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

        _db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            EventType = "Deactivate",
            RecipientAdminId = f.AdminIdA,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.RunAsync();

        result.NotificationsDispatched.Should().Be(0);
        result.AuditRowsProcessed.Should().Be(1);
    }
}
