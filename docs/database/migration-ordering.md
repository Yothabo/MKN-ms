# Migration Ordering

*The order in which the frozen specification's schema changes are applied to the database. Derived from the specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states the order in which the schema changes required by the frozen specification are applied. The order is not arbitrary: the additions have dependencies, and applying them out of order leaves the schema in a state that fails a constraint or blocks a later step.

The document describes *what* is applied, in *what order*. The mechanism — one migration or several, EF Core migrations, SQL scripts, or another — is implementation choice.

---

## The initial state

The database schema is currently the one produced by the single existing migration `20261005211358_InitialCreate`. That migration reflects the pre-amendment entities: `Member.IsActive`, `Event.Location`, `EventDuty.Label`, and no entities from the A–G or attendance amendment sets.

Nothing about the initial schema is wrong. It is simply behind the frozen specification.

The initial schema is identified by the migration that produced it, not by a git commit. Git history and schema state are separate concerns; a migration name is the accurate identifier of a schema state.

---

## The target state

The frozen specification defines a schema larger than the initial one. The additions fall into four categories:

- **New entities.** Nine from the A–G set, three from the attendance set.
- **New columns on existing tables.**
- **Removed columns.**
- **New constraints and checks.**

---

## The ordering

The order below is stated as a sequence. Each step depends on the steps before it.

**Step 1 — new lookup and vocabulary entities.**

Create the entities that have no dependents:

- `MemberStatus`
- `AttributeType`
- `Capability`

These have no foreign keys to anything else and can be created in any order among themselves.

**Step 2 — new entities that depend on Step 1.**

- `AttendanceRule` (references `MemberStatus` through `OutcomeStatusID`)
- `MemberAttributeValue` (references `Member` and `AttributeType`)

`MemberAttributeValue` requires `AttributeType` from Step 1. `AttendanceRule` requires `MemberStatus` from Step 1.

**Step 3 — new entities that reference existing entities.**

- `EventBranch` (references `Event` and `Branch`)
- `Readmission` (references `Member` and `Admin`)
- `NotificationSubscription` (references `PermissionTier` and `Admin`)
- `EntityDeletionPolicy` (references `PermissionTier`)
- `ConfigurationAuditLog` (references `Admin`)

These reference existing tables but are not referenced by any other new entity.

**Step 4 — new entities that depend on Step 3.**

- `AttendanceRuleScope` (references `AttendanceRule` from Step 2)

**Step 5 — column additions on existing tables.**

The additions that do not affect existing constraints:

- `Role.IsDefault`
- `DutyRule.ServiceDefID`
- `ProgramItem.Location`
- `AttendanceRecord.Source`
- `Branch.UsesAttendanceRegister`
- `Event.UsesAttendanceRegister`
- `Member.MemberStatusID` (as a nullable column at this stage; Step 7 makes it non-null)
- `Member.ReceiptNumber`, `Member.CardNumber`, `Member.JoinReason`
- `Event.HostBranchID` (as nullable at this stage; Step 7 makes it non-null)
- `EventDuty.DutyID` (as nullable at this stage; Step 7 makes it non-null)
- `EventDuty.ServiceDefID`
- `Admin.IsActive` (boolean, default true for existing rows)
- `IsDeleted` on every configuration entity that gained the two-flag lifecycle

**Step 6 — data migration for the columns that replace others.**

These steps alter existing data, not just schema:

- Populate `Member.MemberStatusID` for every existing member. The status is chosen by the migration script; a sensible default is the row whose `Name` is "Active" or the row that the admin later designates as the default. The migration is where the choice is made; the specification does not prescribe a mapping for existing rows.
- Populate `Event.HostBranchID` for every existing event. The pre-amendment schema had `Event.Location` as free text. Migration to `HostBranchID` requires the admin to select a branch. The migration may leave `HostBranchID` null temporarily and backfill after the admin has chosen. The specification does not prescribe a mapping for existing rows.
- Migrate `EventDuty.Label` to `EventDuty.DutyID`. Pre-amendment `EventDuty` rows carried a free-text `Label`. Post-amendment, they reference a `Duty`. The migration either creates a Duty row for each distinct Label or leaves the `DutyID` null. The specification does not prescribe a mapping for existing rows.

**Step 7 — constraints on the added columns.**

- Make `Member.MemberStatusID` non-null.
- Make `Event.HostBranchID` non-null.
- Make `EventDuty.DutyID` non-null.
- Add the `AttendanceRuleScope (AttendanceRuleID, CriteriaType, CriteriaValue)` composite primary key.
- Add the check constraint forbidding `IsActive = true AND IsDeleted = true` on every configuration entity that carries both.
- Add the check on `ServiceOccurrence`: exactly one of `ScheduleID` or `EventID` is non-null.

The four uniqueness constraints from the original migration are already present:

- `RosterAssignment (MemberID, DutyID, OccurrenceID)` unique.
- `AttendanceRecord (MemberID, OccurrenceID)` unique.
- `ServiceSchedule (ServiceDefID, TimeSlotID)` active-row unique.
- `ServiceOccurrence (ScheduleID, Date)` schedule-sourced unique.

**Step 8 — removed columns.**

- Drop `Member.IsActive`.
- Drop `Event.Location`.
- Drop `EventDuty.Label`.

Removal happens after the data migration in Step 6 has ensured the replacing columns carry the data the old columns held.

**Step 9 — data integrity re-validation.**

Every surviving row is checked against every constraint added in Step 7. If any row violates a constraint, the migration fails and the failing rows are identified. The specification does not prescribe a repair strategy; the admin decides.

**Step 10 — seed data.**

Seed scripts are re-written against the frozen schema. See `seed-migration.md`.

---

## What the ordering guarantees

Applying the steps in the order stated leaves the schema in the target state. Applying them out of order produces one of the following failures:

- **Foreign key violation.** A new entity references a table that does not yet exist.
- **NOT NULL violation.** A column is made non-null before its data migration.
- **Unique violation.** A uniqueness constraint is added before duplicate rows are resolved.
- **Check violation.** A check constraint is added before its data migration.

The order eliminates each.

---

## What the ordering does not prescribe

- The mechanism. One migration or ten. EF Core `dotnet ef migrations add` calls or hand-written SQL. The specification does not care.
- The naming of the steps. The numbering above is for reference.
- The data migration mapping for pre-amendment rows. This is an administrative decision.
- The transaction boundary per step. Each step should be atomic, but the mechanism is implementation choice.
- Whether steps can be combined. Two adjacent steps with no dependency can be applied as one.

---

*Source: System Design Specification §4, §15; the current migration state.*
