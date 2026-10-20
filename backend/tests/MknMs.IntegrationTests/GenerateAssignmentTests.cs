using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using MknMs.Application.Common;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 5.0 Generate Assignment.
/// </summary>
/// <remarks>
/// Runs against a real PostgreSQL database named mkn_test.
///
/// Verifies:
///   - Duty Rule ranking + Eligibility gate both participate.
///   - A duty with eligible members but no matching tier remains
///     unfilled.
///   - A duty with an eligible member matching its tier is filled.
///   - Inserted assignments carry the correct provenance fields.
///   - Idempotency: a second run creates no additional rows.
///
/// Reference: docs/processes/operations/5.0-generate-assignment.md,
/// Specification §5 and §10.
/// </remarks>
public class GenerateAssignmentTests : IntegrationTestBase
{
    private readonly IDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(
        new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

    protected override async Task OnInitializedAsync()
    {
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    [Fact]
    public async Task Run_WithMixedEligibilityAndTier_ProducesExpectedOutcome()
    {
        var fixture = await SeedFixtureAsync();
        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);

        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesProcessed.Should().Be(1);

        // Reading duty — Beatrice is eligible and matches Tier 1
        // (Role=Steward). One assignment expected.
        // Welcome duty — Dana is the only eligible member, but Dana is
        // a Steward and the Tier 1 rule requires Coordinator. No tier
        // matches. No assignment.
        result.AssignmentsCreated.Should().Be(1);
        result.DutiesFullyFilled.Should().Be(1);
        result.DutiesUnfilled.Should().Be(1);
        result.DutiesPartiallyFilled.Should().Be(0);

        var assignments = await Db.RosterAssignments.ToListAsync();
        assignments.Should().HaveCount(1);

        var reading = assignments.Single();
        reading.DutyId.Should().Be(fixture.ReadingDutyId);
        reading.MemberId.Should().Be(fixture.BeatriceMemberId);
        reading.OccurrenceId.Should().Be(fixture.OccurrenceId);
        reading.AssignmentStatusId.Should().BeNull();
        reading.AssignmentSource.Should().Be("Automatic");
        reading.AssignedBy.Should().BeNull();
        reading.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task Run_Twice_IsIdempotent()
    {
        var fixture = await SeedFixtureAsync();
        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);

        var first = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        var countAfterFirst = await Db.RosterAssignments.CountAsync();

        var second = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        var countAfterSecond = await Db.RosterAssignments.CountAsync();

        first.AssignmentsCreated.Should().Be(1);
        second.AssignmentsCreated.Should().Be(0);
        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task Run_WithNoEligibility_ProducesNoAssignments()
    {
        var fixture = await SeedFixtureAsync(includeEligibility: false);
        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);

        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsCreated.Should().Be(0);

        var assignments = await Db.RosterAssignments.CountAsync();
        assignments.Should().Be(0);
    }

    [Fact]
    public async Task Run_WithInvalidCommand_ReturnsFailure()
    {
        await SeedFixtureAsync();
        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);

        // Neither occurrence nor range supplied.
        var result = await service.RunAsync(new GenerateAssignmentCommand());

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int OccurrenceId,
        int ReadingDutyId,
        int BeatriceMemberId);

    private async Task<Fixture> SeedFixtureAsync(bool includeEligibility = true)
    {
        // Lookups
        var outcomeState = new OutcomeState { Name = "Unfilled" };
        Db.OutcomeStates.Add(outcomeState);

        var serviceType = new ServiceType { Name = "Regular" };
        Db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        Db.TimeOfDays.Add(timeOfDay);

        // Roles
        var coordinator = new Role { Name = "Coordinator", IsActive = true };
        var steward = new Role { Name = "Steward", IsActive = true };
        Db.Roles.Add(coordinator);
        Db.Roles.Add(steward);

        // Duties
        var welcomeDuty = new Duty { Name = "Welcome duty", IsActive = true };
        var readingDuty = new Duty { Name = "Reading duty", IsActive = true };
        Db.Duties.Add(welcomeDuty);
        Db.Duties.Add(readingDuty);

        // Branch
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        Db.Branches.Add(branch);

        // Permission tier + admin
        var tier = new PermissionTier { Name = "Full Admin" };
        Db.PermissionTiers.Add(tier);

        await Db.SaveChangesAsync();

        // Members
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var alex = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = coordinator.RoleId,
            MemberStatusId = statusId,
        };
        var beatrice = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            MemberStatusId = statusId,
        };
        var dana = new Member
        {
            Name = "Dana", Surname = "Prototype",
            JoinDate = new DateOnly(2023, 4, 20),
            DateOfBirth = new DateOnly(1985, 11, 30),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0003", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(alex);
        Db.Members.Add(beatrice);
        Db.Members.Add(dana);

        await Db.SaveChangesAsync();

        // Admin row for Alex.
        var admin = new Admin
        {
            MemberId = alex.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        // Time slot, service definition, schedule.
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        // Attach duties to the definition.
        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = welcomeDuty.DutyId,
            RequiredSlotCount = 1,
        });
        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = readingDuty.DutyId,
            RequiredSlotCount = 1,
        });

        // The schedule.
        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        // Duty rules.
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = welcomeDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Coordinator",
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = readingDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
        });
        await Db.SaveChangesAsync();

        // Eligibility.
        if (includeEligibility)
        {
            Db.Eligibilities.Add(new Eligibility
            {
                MemberId = dana.MemberId,
                DutyId = welcomeDuty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            Db.Eligibilities.Add(new Eligibility
            {
                MemberId = beatrice.MemberId,
                DutyId = readingDuty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            await Db.SaveChangesAsync();
        }

        // The occurrence to fill.
        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        return new Fixture(
            occurrence.OccurrenceId,
            readingDuty.DutyId,
            beatrice.MemberId);
    }

    // -----------------------------------------------------------------
    // Extended criteria coverage — added to satisfy the behavioural
    // coverage the specification's criteria vocabulary implies.
    // -----------------------------------------------------------------

    [Fact]
    public async Task Run_WithGenderCriterion_MatchesOnlyMatchingGender()
    {
        var fixture = await SeedFixtureAsync();

        // Replace Reading duty's Tier 1 rule with Gender=Female. Both
        // Beatrice and Dana are Female, but only Beatrice is eligible.
        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "Gender";
        rule.CriteriaValue = "Female";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Reading duty filled by Beatrice (eligible and Female).
        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithMembershipStageCriterion_MatchesOnlyMatchingStage()
    {
        var fixture = await SeedFixtureAsync();

        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "MembershipStage";
        rule.CriteriaValue = "Full";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithAgeRangeCriterion_MatchingRange()
    {
        var fixture = await SeedFixtureAsync();

        // Beatrice born 1995-03-12. At the fixed test clock (2026-10-06)
        // she is 31. A range of 25-35 matches her.
        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "AgeRange";
        rule.CriteriaValue = "25-35";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithAgeRangeCriterion_NonMatchingRange_SlotUnfilled()
    {
        var fixture = await SeedFixtureAsync();

        // Beatrice is 31. A range of 5-10 does not match.
        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "AgeRange";
        rule.CriteriaValue = "5-10";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Reading duty unfilled; Welcome duty unfilled as well.
        result.DutiesUnfilled.Should().Be(2);
    }

    [Fact]
    public async Task Run_WithTenureCriterion_MatchingThreshold()
    {
        var fixture = await SeedFixtureAsync();

        // Beatrice joined 2024-06-01. At 2026-10-06 she has ~857 days.
        // A 30-day threshold matches.
        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "Tenure";
        rule.CriteriaValue = "30";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithTwoTiers_FallsThroughToSecondTier()
    {
        var fixture = await SeedFixtureAsync();

        // Replace Reading duty's rule set with two tiers:
        //   Tier 1: Role=Coordinator (no eligible match)
        //   Tier 2: Role=Steward (Beatrice matches)
        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Coordinator",
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 2,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Reading duty filled by Beatrice via Tier 2.
        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithSameTierTwoRulesAndsThem()
    {
        var fixture = await SeedFixtureAsync();

        // Tier 1 for Reading duty: Role=Steward AND Gender=Female.
        // Beatrice matches both; Dana matches both but is not eligible.
        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "Gender",
            CriteriaValue = "Female",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Beatrice matches both criteria; Reading filled.
        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_WithSameTierTwoRules_AndFails_NoCandidate()
    {
        var fixture = await SeedFixtureAsync();

        // Tier 1 for Reading duty: Role=Steward AND Gender=Male.
        // Beatrice is Female. No match.
        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "Gender",
            CriteriaValue = "Male",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Both duties unfilled.
        result.DutiesUnfilled.Should().Be(2);
    }

    [Fact]
    public async Task Run_WithReservedCriterion_ProducesNoCandidates()
    {
        var fixture = await SeedFixtureAsync();

        var rule = await Db.DutyRules
            .FirstAsync(r => r.DutyId == fixture.ReadingDutyId);
        rule.CriteriaType = "AcceptanceRate";
        rule.CriteriaValue = "0.5";
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Reserved criteria are not evaluated. No candidates. Unfilled.
        result.DutiesUnfilled.Should().Be(2);
    }

    [Fact]
    public async Task Run_BranchAttendanceRecency_RegisterOffAtBranch_CriterionSkipped()
    {
        // §10.1.5: Branch-Attendance Recency is skipped when the
        // effective register scope is off. §9.6: the branch of the
        // occurrence's time slot controls the schedule-sourced scope.
        var fixture = await SeedFixtureAsync();

        // Turn the register off at the branch the occurrence is at.
        var branch = await Db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = false;
        await Db.SaveChangesAsync();

        // Replace Reading duty's rule with a Branch-Attendance
        // Recency criterion that would otherwise gate on attendance.
        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // The criterion is skipped, so the tier matches the eligible
        // member.
        result.DutiesFullyFilled.Should().Be(1);
    }

    [Fact]
    public async Task Run_BranchAttendanceRecency_RegisterOnAtBranch_CriterionGates()
    {
        // Same setup, register on. Without matching attendance, the
        // criterion produces no candidate.
        var fixture = await SeedFixtureAsync();

        var branch = await Db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = true;
        await Db.SaveChangesAsync();

        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // No matching attendance, so the tier produces no candidate.
        result.DutiesUnfilled.Should().BeGreaterOrEqualTo(1);
    }


    // -----------------------------------------------------------------
    // G3-5.0 — acceptance-matrix coverage
    // -----------------------------------------------------------------

    [Fact]
    public async Task Run_WithThreeEligibleOneSlot_AssignsLowestMemberId()
    {
        var fixture = await SeedFixtureAsync();

        // Two more Steward-role members, both eligible for Reading.
        var stewardRoleId = await Db.Roles
            .Where(r => r.Name == "Steward")
            .Select(r => r.RoleId)
            .FirstAsync();

        var statusId = await Db.Members
            .Select(m => m.MemberStatusId)
            .FirstAsync();

        var branchId = await Db.Members
            .Select(m => m.BranchId)
            .FirstAsync();

        var extraOne = new MknMs.Domain.D4_Member.Member
        {
            Name = "Extra", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-100-0000", Email = null,
            BranchId = branchId, RoleId = stewardRoleId,
            MemberStatusId = statusId,
        };
        var extraTwo = new MknMs.Domain.D4_Member.Member
        {
            Name = "Extra", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-100-0001", Email = null,
            BranchId = branchId, RoleId = stewardRoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(extraOne);
        Db.Members.Add(extraTwo);
        await Db.SaveChangesAsync();

        var adminId = await Db.Admins.Select(a => a.AdminId).FirstAsync();

        // The fixture already grants Beatrice eligibility for Reading.
        // Beatrice was seeded first among the Stewards, so her MemberID
        // is the lowest of the three eligible candidates. ExtraOne and
        // ExtraTwo were added later, so their MemberIDs are higher.
        Db.Eligibilities.Add(new MknMs.Domain.D6_Eligibility.Eligibility
        {
            MemberId = extraOne.MemberId,
            DutyId = fixture.ReadingDutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        Db.Eligibilities.Add(new MknMs.Domain.D6_Eligibility.Eligibility
        {
            MemberId = extraTwo.MemberId,
            DutyId = fixture.ReadingDutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Reading has one slot and three eligible Stewards. B5: the
        // lowest MemberID wins. Beatrice was seeded first, so she is the
        // lowest.
        var readingAssignment = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();

        readingAssignment.Should().HaveCount(1);
        readingAssignment.Single().MemberId.Should().Be(fixture.BeatriceMemberId);

        // Assert the exact candidate ordering: Beatrice < ExtraOne < ExtraTwo.
        fixture.BeatriceMemberId.Should().BeLessThan(extraOne.MemberId);
        extraOne.MemberId.Should().BeLessThan(extraTwo.MemberId);
    }

    [Fact]
    public async Task Run_WithFiveEligibleThreeSlots_AssignsLowestThree()
    {
        var fixture = await SeedFixtureAsync();

        var stewardRoleId = await Db.Roles
            .Where(r => r.Name == "Steward")
            .Select(r => r.RoleId)
            .FirstAsync();
        var statusId = await Db.Members.Select(m => m.MemberStatusId).FirstAsync();
        var branchId = await Db.Members.Select(m => m.BranchId).FirstAsync();

        // Raise Reading's slot count to 3.
        var readingSdd = await Db.ServiceDefinitionDuties
            .FirstAsync(sd => sd.DutyId == fixture.ReadingDutyId);
        readingSdd.RequiredSlotCount = 3;
        await Db.SaveChangesAsync();

        var extras = new List<MknMs.Domain.D4_Member.Member>();
        for (var i = 0; i < 4; i++)
        {
            var m = new MknMs.Domain.D4_Member.Member
            {
                Name = "Candidate", Surname = $"Number{i}",
                JoinDate = new DateOnly(2024, 1, 1),
                DateOfBirth = new DateOnly(1990, 1, 1),
                MembershipStage = "Full", Gender = "Other",
                Phone = $"+27-200-{i:D4}", Email = null,
                BranchId = branchId, RoleId = stewardRoleId,
                MemberStatusId = statusId,
            };
            extras.Add(m);
            Db.Members.Add(m);
        }
        await Db.SaveChangesAsync();

        var adminId = await Db.Admins.Select(a => a.AdminId).FirstAsync();
        foreach (var m in extras)
        {
            Db.Eligibilities.Add(new MknMs.Domain.D6_Eligibility.Eligibility
            {
                MemberId = m.MemberId,
                DutyId = fixture.ReadingDutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = adminId,
                RevokedDate = null,
                RevokedReason = null,
            });
        }
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Five eligible Stewards — Beatrice plus four new — compete for
        // three slots. B5: the three lowest MemberIDs are assigned.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .OrderBy(a => a.MemberId)
            .ToListAsync();

        readingAssignments.Should().HaveCount(3);

        var expected = new List<int>
        {
            fixture.BeatriceMemberId,
            extras[0].MemberId,
            extras[1].MemberId,
        }.OrderBy(id => id).ToList();

        readingAssignments.Select(a => a.MemberId).Should().Equal(expected);
    }

    [Fact]
    public async Task Run_WithNonTerminalAssignmentOnOccurrence_ExcludesMemberFromOtherDuties()
    {
        var fixture = await SeedFixtureAsync();

        // Seed a non-terminal assignment for Beatrice on the Welcome
        // duty. She is eligible for Reading too, but §10.1.2 excludes a
        // member who already holds a non-terminal assignment on the
        // occurrence.
        var welcomeDutyId = await Db.Duties
            .Where(d => d.Name == "Welcome duty")
            .Select(d => d.DutyId)
            .FirstAsync();

        var proposed = new MknMs.Domain.D10_ConfigLookups.AssignmentStatus
        {
            Name = "Proposed (coverage test)",
            IsTerminal = false,
        };
        Db.AssignmentStatuses.Add(proposed);
        await Db.SaveChangesAsync();

        Db.RosterAssignments.Add(new MknMs.Domain.D7_RosterAssignment.RosterAssignment
        {
            MemberId = fixture.BeatriceMemberId,
            DutyId = welcomeDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = proposed.AssignmentStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Beatrice already occupies a slot. She must not be assigned to
        // Reading.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();

        readingAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WithServiceOccurrenceDutyRemoved_SkipsTheDuty()
    {
        var fixture = await SeedFixtureAsync();

        Db.ServiceOccurrenceDuties.Add(new MknMs.Domain.D3_BranchTimeSlotService.ServiceOccurrenceDuty
        {
            OccurrenceId = fixture.OccurrenceId,
            DutyId = fixture.ReadingDutyId,
            Action = "Removed",
            RequiredSlotCount = null,
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // The Removed override removes the duty from the effective list.
        // No assignment for Reading.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        readingAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WithServiceOccurrenceDutyAddedOverride_UsesOverrideCount()
    {
        var fixture = await SeedFixtureAsync();

        // Add a second slot for Reading via the override.
        Db.ServiceOccurrenceDuties.Add(new MknMs.Domain.D3_BranchTimeSlotService.ServiceOccurrenceDuty
        {
            OccurrenceId = fixture.OccurrenceId,
            DutyId = fixture.ReadingDutyId,
            Action = "Added",
            RequiredSlotCount = 2,
        });
        await Db.SaveChangesAsync();

        // Add a second eligible Steward so both slots can fill.
        var stewardRoleId = await Db.Roles
            .Where(r => r.Name == "Steward")
            .Select(r => r.RoleId)
            .FirstAsync();
        var statusId = await Db.Members.Select(m => m.MemberStatusId).FirstAsync();
        var branchId = await Db.Members.Select(m => m.BranchId).FirstAsync();

        var second = new MknMs.Domain.D4_Member.Member
        {
            Name = "Second", Surname = "Steward",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-300-0000", Email = null,
            BranchId = branchId, RoleId = stewardRoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(second);
        await Db.SaveChangesAsync();

        var adminId = await Db.Admins.Select(a => a.AdminId).FirstAsync();
        Db.Eligibilities.Add(new MknMs.Domain.D6_Eligibility.Eligibility
        {
            MemberId = second.MemberId,
            DutyId = fixture.ReadingDutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();
        readingAssignments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Run_BranchAttendanceRecency_RegisterOnAtBranch_MatchingAttendance_CriterionMatches()
    {
        var fixture = await SeedFixtureAsync();

        var branch = await Db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = true;
        await Db.SaveChangesAsync();

        // Replace Reading's rule with Branch-Attendance Recency.
        var existing = await Db.DutyRules
            .Where(r => r.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        Db.DutyRules.RemoveRange(existing);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new MknMs.Domain.D2_DutyRule.DutyRule
        {
            DutyId = fixture.ReadingDutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
        });
        await Db.SaveChangesAsync();

        // Seed attendance for Beatrice against the occurrence.
        Db.AttendanceRecords.Add(new MknMs.Domain.D8_AttendanceRecord.AttendanceRecord
        {
            MemberId = fixture.BeatriceMemberId,
            OccurrenceId = fixture.OccurrenceId,
            Timestamp = _clock.GetUtcNow(),
            Source = "Manual",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Beatrice's attendance satisfies the criterion; Reading is
        // filled with her.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();
        readingAssignments.Should().HaveCount(1);
        readingAssignments.Single().MemberId.Should().Be(fixture.BeatriceMemberId);
    }

    [Fact]
    public async Task Run_AfterPartialRun_ResumesAndFillsRemainingSlots()
    {
        var fixture = await SeedFixtureAsync();

        // Seed a partial state: Beatrice already holds the Reading slot.
        // Welcome is empty. The fixture seeded both ServiceDefinitionDuty
        // rows as RequiredSlotCount = 1.
        Db.RosterAssignments.Add(new MknMs.Domain.D7_RosterAssignment.RosterAssignment
        {
            MemberId = fixture.BeatriceMemberId,
            DutyId = fixture.ReadingDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // The partial row is preserved, not duplicated. Beatrice holds
        // Reading. Welcome stays unfilled because Dana's role does not
        // match the Welcome tier.
        var reading = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();
        reading.Should().HaveCount(1);
        reading.Single().MemberId.Should().Be(fixture.BeatriceMemberId);

        // The service did not create a duplicate for Beatrice on Reading.
        result.AssignmentsCreated.Should().Be(0);
    }

    [Fact]
    public async Task Run_WithThreeDutiesTwoSlotsEach_AssignsPerDutyIndependently()
    {
        var fixture = await SeedFixtureAsync();

        // Raise both existing duties to two slots and add a third.
        var readingSdd = await Db.ServiceDefinitionDuties
            .FirstAsync(sd => sd.DutyId == fixture.ReadingDutyId);
        readingSdd.RequiredSlotCount = 2;

        var welcomeDutyId = await Db.Duties
            .Where(d => d.Name == "Welcome duty")
            .Select(d => d.DutyId)
            .FirstAsync();
        var welcomeSdd = await Db.ServiceDefinitionDuties
            .FirstAsync(sd => sd.DutyId == welcomeDutyId);
        welcomeSdd.RequiredSlotCount = 2;
        await Db.SaveChangesAsync();

        // Two more Stewards so Reading's two slots can fill.
        var stewardRoleId = await Db.Roles
            .Where(r => r.Name == "Steward")
            .Select(r => r.RoleId)
            .FirstAsync();
        var statusId = await Db.Members.Select(m => m.MemberStatusId).FirstAsync();
        var branchId = await Db.Members.Select(m => m.BranchId).FirstAsync();
        var adminId = await Db.Admins.Select(a => a.AdminId).FirstAsync();

        var stewards = new List<MknMs.Domain.D4_Member.Member>();
        for (var i = 0; i < 2; i++)
        {
            var m = new MknMs.Domain.D4_Member.Member
            {
                Name = "Multi", Surname = $"Steward{i}",
                JoinDate = new DateOnly(2024, 1, 1),
                DateOfBirth = new DateOnly(1990, 1, 1),
                MembershipStage = "Full", Gender = "Other",
                Phone = $"+27-400-{i:D4}", Email = null,
                BranchId = branchId, RoleId = stewardRoleId,
                MemberStatusId = statusId,
            };
            stewards.Add(m);
            Db.Members.Add(m);
        }
        await Db.SaveChangesAsync();

        foreach (var m in stewards)
        {
            Db.Eligibilities.Add(new MknMs.Domain.D6_Eligibility.Eligibility
            {
                MemberId = m.MemberId,
                DutyId = fixture.ReadingDutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = adminId,
                RevokedDate = null,
                RevokedReason = null,
            });
        }
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Reading's fill count is independent of Welcome's. Reading:
        // two slots, filled with the two lowest Stewards — Beatrice and
        // the first new Steward. Welcome: two slots, no matching
        // candidate.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();
        readingAssignments.Should().HaveCount(2);

        var welcomeAssignments = await Db.RosterAssignments
            .Where(a => a.DutyId == welcomeDutyId
                && a.OccurrenceId == fixture.OccurrenceId)
            .ToListAsync();
        welcomeAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WithFailingNotification_AssignmentStillCommitted()
    {
        var fixture = await SeedFixtureAsync();

        var throwing = new ThrowingDispatchNotificationService();
        var service = new GenerateAssignmentService(Db, throwing, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);

        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // B7: the notification failure does not fail the run, does not
        // roll back the assignment, and does not cause a retry.
        result.Status.Should().Be("Success");
        result.AssignmentsCreated.Should().Be(1);

        var assignments = await Db.RosterAssignments.ToListAsync();
        assignments.Should().HaveCount(1);
        assignments.Single().MemberId.Should().Be(fixture.BeatriceMemberId);
    }

    [Fact]
    public async Task Run_OnEventSourcedOccurrence_FillsOnlyAddedDuties()
    {
        var fixture = await SeedFixtureAsync();

        // Create an event-sourced occurrence. ScheduleId = null,
        // EventId set. The Event must exist first.
        var branchId = await Db.Branches.Select(b => b.BranchId).FirstAsync();
        var evt = new MknMs.Domain.D9_EventProgram.Event
        {
            Name = "Coverage event",
            StartDate = new DateOnly(2026, 10, 11),
            EndDate = new DateOnly(2026, 10, 11),
            Type = "Coverage",
            HostBranchId = branchId,
            IsDeleted = false,
        };
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var serviceTypeId = await Db.ServiceTypes.Select(st => st.ServiceTypeId).FirstAsync();

        var occurrence = new MknMs.Domain.D3_BranchTimeSlotService.ServiceOccurrence
        {
            ScheduleId = null,
            EventId = evt.EventId,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = serviceTypeId,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = await Db.Admins.Select(a => a.AdminId).FirstAsync(),
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        // Add Reading as an Added override on the event-sourced
        // occurrence. Welcome is not added, so it is not considered —
        // event-sourced occurrences inherit nothing from a schedule.
        Db.ServiceOccurrenceDuties.Add(new MknMs.Domain.D3_BranchTimeSlotService.ServiceOccurrenceDuty
        {
            OccurrenceId = occurrence.OccurrenceId,
            DutyId = fixture.ReadingDutyId,
            Action = "Added",
            RequiredSlotCount = 1,
        });
        await Db.SaveChangesAsync();

        var genService = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await genService.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrence.OccurrenceId,
        });

        result.Status.Should().Be("Success");

        // Only the Added duty is filled. Welcome — the fixture's other
        // duty — is not considered because no schedule contributed it.
        var readingAssignments = await Db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrence.OccurrenceId
                && a.DutyId == fixture.ReadingDutyId)
            .ToListAsync();
        readingAssignments.Should().HaveCount(1);

        var welcomeDutyId = await Db.Duties
            .Where(d => d.Name == "Welcome duty")
            .Select(d => d.DutyId)
            .FirstAsync();
        var welcomeAssignments = await Db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrence.OccurrenceId
                && a.DutyId == welcomeDutyId)
            .ToListAsync();
        welcomeAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_WithTerminalAssignmentOnOccurrence_DoesNotBlockAvailability()
    {
        var fixture = await SeedFixtureAsync();

        // Insert a terminal assignment for Beatrice on Welcome duty.
        // Terminal statuses do not count toward capacity or availability.
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        Db.AssignmentStatuses.Add(declined);
        await Db.SaveChangesAsync();

        var welcomeDutyId = await Db.Duties
            .Where(d => d.Name == "Welcome duty")
            .Select(d => d.DutyId)
            .FirstAsync();

        Db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = fixture.BeatriceMemberId,
            DutyId = welcomeDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = declined.AssignmentStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        // Beatrice is still available for Reading duty — her terminal
        // assignment on Welcome does not block.
        result.DutiesFullyFilled.Should().Be(1);
    }
}
