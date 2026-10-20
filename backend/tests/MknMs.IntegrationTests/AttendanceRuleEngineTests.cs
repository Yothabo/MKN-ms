using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.AttendanceRuleEngine;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D13_AttendanceRule;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D8_AttendanceRecord;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 13.0 Attendance Rule Engine.
/// </summary>
/// <remarks>
/// Runs against mkn_test. Covers the acceptance-matrix rows for
/// 13.0: absence window boundaries, register scope, evaluation
/// order, outcome vocabulary, manual invocation, and required
/// settings.
/// </remarks>
public class AttendanceRuleEngineTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int BranchId,
        int RoleId,
        int DefaultStatusId,
        int OtherStatusId,
        int MemberId,
        int AdminId);

    private async Task<Fixture> SeedAsync(bool registerEnabled = true)
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var role = new Role { Name = "Member", IsDeleted = false };
        var branch = new Branch
        {
            Name = "Main",
            Location = "Main Street",
            IsDeleted = false,
            UsesAttendanceRegister = null,
        };
        var defaultStatusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var otherStatus = new MemberStatus
        {
            Name = "Inactive", IsRosterable = false, IsDefault = false, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.ServiceTypes.Add(serviceType);
        Db.TimeOfDays.Add(timeOfDay);
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.MemberStatuses.Add(otherStatus);
        Db.PermissionTiers.Add(tier);

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone",
            Value = "Africa/Johannesburg",
            Required = true,
        });
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "AttendanceRegisterEnabled",
            Value = registerEnabled ? "true" : "false",
            Required = true,
        });

        await Db.SaveChangesAsync();

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId,
            RoleId = role.RoleId,
            MemberStatusId = defaultStatusId,
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

        return new Fixture(
            branch.BranchId,
            role.RoleId,
            defaultStatusId,
            otherStatus.MemberStatusId,
            member.MemberId,
            admin.AdminId);
    }

    private async Task<int> SeedOccurrenceWithAttendanceAsync(
        Fixture f,
        DateOnly date)
    {
        // A schedule and occurrence, so an AttendanceRecord can point
        // at a real occurrence.
        var slot = await Db.BranchTimeSlots
            .FirstOrDefaultAsync(bts => bts.BranchId == f.BranchId);

        if (slot is null)
        {
            var timeOfDayId = await Db.TimeOfDays
                .Select(t => t.TimeOfDayId).FirstAsync();
            slot = new BranchTimeSlot
            {
                BranchId = f.BranchId,
                DayOfWeek = "Sunday",
                TimeOfDayId = timeOfDayId,
            };
            Db.BranchTimeSlots.Add(slot);
            await Db.SaveChangesAsync();
        }

        var serviceTypeId = await Db.ServiceTypes
            .Select(st => st.ServiceTypeId).FirstAsync();

        var definition = new ServiceDefinition
        {
            Name = "Svc",
            ServiceTypeId = serviceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = date,
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        return occurrence.OccurrenceId;
    }

    private async Task AddAttendanceAsync(int memberId, int occurrenceId)
    {
        Db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = occurrenceId,
            Timestamp = Now,
            Source = "Manual",
        });
        await Db.SaveChangesAsync();
    }

    private async Task<int> AddRuleAsync(
        string name,
        string triggerType,
        string? triggerValue,
        string outcomeType,
        int? outcomeStatusId = null,
        bool enabled = true)
    {
        var rule = new AttendanceRule
        {
            Name = name,
            TriggerType = triggerType,
            TriggerValue = triggerValue,
            OutcomeType = outcomeType,
            OutcomeStatusId = outcomeStatusId,
            Enabled = enabled,
            IsDeleted = false,
        };
        Db.AttendanceRules.Add(rule);
        await Db.SaveChangesAsync();
        return rule.AttendanceRuleId;
    }

    // -----------------------------------------------------------------
    // AbsenceDays
    // -----------------------------------------------------------------

    [Fact]
    public async Task AbsenceDays_91Days_NoAttendance_Fires()
    {
        var f = await SeedAsync();
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(1);
        result.StatusChangesApplied.Should().Be(1);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    [Fact]
    public async Task AbsenceDays_ExactBoundary_AttendanceOnBoundaryDay_DoesNotFire()
    {
        var f = await SeedAsync();
        // Boundary: today - 90. An attendance record on that date
        // satisfies the window because the window is inclusive.
        var boundaryDate = DateOnly.FromDateTime(Now.Date).AddDays(-90);
        var occId = await SeedOccurrenceWithAttendanceAsync(f, boundaryDate);
        await AddAttendanceAsync(f.MemberId, occId);

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task AbsenceDays_AttendanceOutsideWindow_Fires()
    {
        var f = await SeedAsync();
        // Attendance 91 days ago is outside the [today-90, today]
        // window.
        var outsideDate = DateOnly.FromDateTime(Now.Date).AddDays(-91);
        var occId = await SeedOccurrenceWithAttendanceAsync(f, outsideDate);
        await AddAttendanceAsync(f.MemberId, occId);

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // Register scope
    // -----------------------------------------------------------------

    [Fact]
    public async Task AbsenceDays_RegisterOffAtBranch_IsSkipped()
    {
        var f = await SeedAsync(registerEnabled: false);
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task BranchOverride_OnWithGlobalOff_Applies()
    {
        var f = await SeedAsync(registerEnabled: false);

        var branch = await Db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = true;
        await Db.SaveChangesAsync();

        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // Evaluation order (B8)
    // -----------------------------------------------------------------

    [Fact]
    public async Task TwoSetStatusRules_LaterWins()
    {
        var f = await SeedAsync();
        await AddRuleAsync("first", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);
        await AddRuleAsync("second", "AbsenceDays", "90", "SetStatus", f.DefaultStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(2);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task SetStatusAndNotify_BothTakeEffect()
    {
        var f = await SeedAsync();
        await AddRuleAsync("status", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);
        await AddRuleAsync("notify", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
        result.NotificationsDispatched.Should().Be(1);
    }

    [Fact]
    public async Task TwoNotifyRules_TwoDispatches()
    {
        var f = await SeedAsync();
        await AddRuleAsync("notify-1", "AbsenceDays", "90", "Notify");
        await AddRuleAsync("notify-2", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.NotificationsDispatched.Should().Be(2);
    }

    [Fact]
    public async Task SetStatus_OnMemberAlreadyInTargetStatus_StateUnchanged()
    {
        // The member is already in the target status. The rule fires
        // and the engine applies the write; the resulting state is
        // unchanged. The counter records the write, not the
        // difference. §13.6.
        var f = await SeedAsync();
        await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.DefaultStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    // -----------------------------------------------------------------
    // Idempotency (B10)
    // -----------------------------------------------------------------

    [Fact]
    public async Task RepeatedRun_TriggerStillHolds_OutcomeReapplied()
    {
        var f = await SeedAsync();
        await AddRuleAsync("notify", "AbsenceDays", "90", "Notify");

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var first = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });
        var second = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        first.NotificationsDispatched.Should().Be(1);
        second.NotificationsDispatched.Should().Be(1);
    }

    // -----------------------------------------------------------------
    // Manual rules (B11)
    // -----------------------------------------------------------------

    [Fact]
    public async Task ManualRule_NotEvaluatedByScheduledRun()
    {
        var f = await SeedAsync();
        await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(0);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_FiresUnconditionally()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", "999", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(1);
        result.StatusChangesApplied.Should().Be(1);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    [Fact]
    public async Task ManualInvocation_NonManualRule_Rejected()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("absent", "AbsenceDays", "90", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Attendance Rule");
    }

    [Fact]
    public async Task ManualInvocation_NonexistentAdmin_Fails()
    {
        // §13.6: the manual invocation carries the ActingAdminID of the
        // administrator performing it, which resolves to an Admin row.
        // A positive id that names no row does not satisfy the
        // requirement.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = 9999,
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("not_found");
        result.ErrorDetail.Should().Contain("Admin 9999");

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_InactiveAdmin_Fails()
    {
        // §13.6: the ActingAdminID resolves to an Admin row whose
        // IsActive = true. An inactive admin does not satisfy the
        // requirement.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("not_found");
        result.ErrorDetail.Should().Contain("is not active");

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task ManualInvocation_TwiceSameDay_OutcomeAppliedTwice()
    {
        // §13.6: repeat invocation applies the outcome again; the
        // engine does not maintain per-rule execution history.
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync("manual", "Manual", null, "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);

        var first = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });
        var second = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        first.Status.Should().Be("Success");
        first.StatusChangesApplied.Should().Be(1);
        second.Status.Should().Be("Success");
        second.StatusChangesApplied.Should().Be(1);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.OtherStatusId);
    }

    // -----------------------------------------------------------------
    // ReadmissionCount
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReadmissionCount_AtThreshold_Fires()
    {
        var f = await SeedAsync();

        Db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        Db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 6, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await Db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "2", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
    }

    [Fact]
    public async Task ReadmissionCount_BelowThreshold_DoesNotFire()
    {
        var f = await SeedAsync();

        Db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await Db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "2", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);
    }

    [Fact]
    public async Task ReadmissionCount_RegisterOff_StillApplies()
    {
        var f = await SeedAsync(registerEnabled: false);

        Db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await Db.SaveChangesAsync();

        await AddRuleAsync("readmit", "ReadmissionCount", "1", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);
    }

    [Fact]
    public async Task Engine_DoesNotWriteReadmission()
    {
        // §13.6: the engine reads Readmission and never writes it.
        // 3.0 owns Readmission creation.
        var f = await SeedAsync();

        Db.Readmissions.Add(new Readmission
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2025, 1, 1),
            PerformedByAdminId = f.AdminId,
            Reason = null,
        });
        await Db.SaveChangesAsync();

        var before = await Db.Readmissions.CountAsync();
        before.Should().Be(1);

        await AddRuleAsync("readmit", "ReadmissionCount", "1", "SetStatus", f.OtherStatusId);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(1);

        var after = await Db.Readmissions.CountAsync();
        after.Should().Be(before);
    }

    // -----------------------------------------------------------------
    // Required settings
    // -----------------------------------------------------------------

    [Fact]
    public async Task MissingAttendanceRegisterEnabled_Fails()
    {
        var f = await SeedAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "AttendanceRegisterEnabled");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_missing");
    }

    [Fact]
    public async Task MissingApplicationTimeZone_Fails()
    {
        var f = await SeedAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "ApplicationTimeZone");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Failure");
        result.ErrorCode.Should().Be("configuration_missing");
    }

    // -----------------------------------------------------------------
    // Disabled rule
    // -----------------------------------------------------------------

    [Fact]
    public async Task DisabledRule_NotEvaluated()
    {
        var f = await SeedAsync();
        await AddRuleAsync("disabled", "AbsenceDays", "90", "SetStatus",
            f.OtherStatusId, enabled: false);

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
        });

        result.Status.Should().Be("Success");
        result.RulesEvaluated.Should().Be(0);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }
    [Fact]
    public async Task SetStatus_OutcomeStatusSoftDeleted_RuleSkipped()
    {
        var f = await SeedAsync();
        var ruleId = await AddRuleAsync(
            "status", "Manual", null, "SetStatus", f.OtherStatusId);

        // Soft-delete the outcome status. The rule was valid at write
        // time; the status is now soft-deleted. The engine must skip
        // the outcome rather than write a soft-deleted status.
        var status = await Db.MemberStatuses
            .FirstAsync(s => s.MemberStatusId == f.OtherStatusId);
        status.IsDeleted = true;
        status.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new AttendanceRuleEngineService(Db, _notification, _clock);
        var result = await service.RunAsync(new AttendanceRuleEngineCommand
        {
            TriggerType = "Manual",
            RuleId = ruleId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.StatusChangesApplied.Should().Be(0);

        var member = await Db.Members.FirstAsync();
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }
}
