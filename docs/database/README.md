# Database Documentation

*The implementation-facing reference for the system's data model. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This directory contains the implementation-facing reference for the system's logical data model. It describes the schema, its constraints, its settings, its candidate indexes, and the consolidated list of amendments the locked contracts imply.

It is derived from `../spec/system-design-spec.md` and does not extend it. Where a conflict arises, the specification wins.

---

## Documents

### `schema.md`

Every table, every column, its type, its nullability, and its notes. The complete logical data model in one place.

### `constraints.md`

Every constraint the system requires — uniqueness rules, foreign keys, and check constraints. Each is sourced to the specification section that locks it.

### `settings.md`

Every `SystemSetting` key the specification defines. Its type, its requiredness, its consumer, and its behavior when absent.

### `indexes.md`

Three categories of index information:

1. **Required logical constraints and access patterns** — things the specification locks.
2. **Candidate indexes** — indexes that may be warranted based on the access patterns, but are not prescribed at the specification level.
3. **Physical indexes** — the indexes actually chosen during implementation against the target database.

The distinction is deliberate. The specification leaves some physical choices implementation-dependent.

### `amendments.md`

The consolidated list of schema amendments implied by the locked operational contracts (§7 through §14). Two columns, four constraints, eight settings, one nullability clarification. Each is sourced.

---

## What this directory does not contain

This directory describes the **logical** data model. It does not contain:

- Physical DDL for any specific database engine.
- Engine-specific type mappings.
- Actual index DDL.
- Migration scripts.
- Seed data.

These are implementation artifacts produced against the logical model. They are not part of the specification, and this directory does not prescribe them.

---

## The amendment set at a glance

The base schema is defined in §4 of the System Design Specification. The locked contracts imply a small set of additions, consolidated in `amendments.md`:

**Two new columns:**
- `RosterAssignment.CreatedAt`
- `AssignmentStatus.IsTerminal`

**Four new constraints:**
- `RosterAssignment (MemberID, DutyID, OccurrenceID)` unique
- `AttendanceRecord (MemberID, OccurrenceID)` unique
- `ServiceSchedule (ServiceDefID, TimeSlotID)` unique for active rows
- `ServiceOccurrence (ScheduleID, Date)` unique for schedule-sourced rows

**Eight new settings:**
- `OccurrenceHorizonDays`
- `InitialAssignmentStatusID`
- `ConfirmationTimeoutHours`
- `OutcomeStateUnfilledID`
- `OutcomeStatePartiallyFilledID`
- `OutcomeStateFilledID`
- `OutcomeStateCancelledID`
- `NotificationChannel`

**One nullability clarification:**
- `RosterAssignment.AssignmentStatusID` is nullable

The original set introduced fifteen amendments. Two later sets have been added: the A–G amendment set and the attendance amendment set. Both are documented in `amendments.md`.

---

## Reading order

1. **`schema.md`** — the entities.
2. **`constraints.md`** — the integrity rules.
3. **`settings.md`** — the configuration surface.
4. **`indexes.md`** — the performance strategy.
5. **`amendments.md`** — the delta from the base schema.

---

*Source: System Design Specification §4, §15.*
