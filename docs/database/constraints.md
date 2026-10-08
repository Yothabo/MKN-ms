# Constraints

*Every constraint the system requires. Derived from the System Design Specification §15 and the locked contracts in §7 through §14. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document lists every integrity constraint the system's locked contracts require: uniqueness constraints, foreign keys, and check constraints. Each is sourced.

Constraints fall into three groups:

- **Unique constraints** — physical uniqueness rules enforced at the database level.
- **Foreign keys** — referential integrity between tables.
- **Check constraints** — conditional relationships between columns within a row.

---

## Unique constraints

### `ServiceOccurrence (ScheduleID, Date)` — schedule-sourced rows

| Property | Value |
| --- | --- |
| Table | ServiceOccurrence |
| Columns | (ScheduleID, Date) |
| Scope | Rows where ScheduleID IS NOT NULL |
| Purpose | One occurrence per schedule per date; correctness guard for 11.0 idempotency |
| Source | §7, §8, §15.2.4 |

Event-sourced occurrences (ScheduleID null) are excluded. Their uniqueness is governed by the 8.0 process.

Physical expression: filtered unique index, partial unique index, or NULL-distinct composite unique — depends on target database.

### `ServiceSchedule (ServiceDefID, TimeSlotID)` — active rows

| Property | Value |
| --- | --- |
| Table | ServiceSchedule |
| Columns | (ServiceDefID, TimeSlotID) |
| Scope | Rows where IsActive = true AND IsDeleted = false |
| Purpose | An active schedule is uniquely identified by the pair |
| Source | §9.1.5, §9 D1 lock, §15.2.3 |

Inactive schedules are excluded. A deactivated schedule may be replaced.

### `RosterAssignment (MemberID, DutyID, OccurrenceID)`

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Columns | (MemberID, DutyID, OccurrenceID) |
| Purpose | Prevent duplicate member/duty/occurrence records |
| Source | §10 G2 lock, §15.2.1 |

Applies regardless of AssignmentSource. Prevents both automatic and manual duplicate inserts.

### `AttendanceRecord (MemberID, OccurrenceID)`

| Property | Value |
| --- | --- |
| Table | AttendanceRecord |
| Columns | (MemberID, OccurrenceID) |
| Purpose | At most one recorded attendance fact per member per occurrence |
| Source | §13 A4 lock, §15.2.2 |

A subsequent tap for an existing pair is a no-op.

---

## Foreign keys

### Configuration stores

| From | Column | To |
| --- | --- | --- |
| DutyRule | DutyID | Duty |
| BranchTimeSlot | BranchID | Branch |
| BranchTimeSlot | TimeOfDayID | TimeOfDay |
| ServiceDefinition | ServiceTypeID | ServiceType |
| ServiceDefinition | OwningBranchID | Branch |
| ServiceDefinitionDuty | ServiceDefID | ServiceDefinition |
| ServiceDefinitionDuty | DutyID | Duty |
| ServiceSchedule | ServiceDefID | ServiceDefinition |
| ServiceSchedule | TimeSlotID | BranchTimeSlot |

### Membership and eligibility

| From | Column | To |
| --- | --- | --- |
| Member | BranchID | Branch |
| Member | RoleID | Role |
| IdentifierHistory | MemberID | Member |
| IdentifierHistory | AuthorizedBy | Admin |
| Eligibility | MemberID | Member |
| Eligibility | DutyID | Duty |
| Eligibility | GrantedBy | Admin |
| Admin | MemberID | Member |
| Admin | PermissionTierID | PermissionTier |

### Operational stores

| From | Column | To |
| --- | --- | --- |
| ServiceOccurrence | ScheduleID | ServiceSchedule |
| ServiceOccurrence | EventID | Event |
| ServiceOccurrence | FillStatusID | OutcomeState |
| ServiceOccurrence | CreatedBy | Admin |
| ServiceOccurrence | ChangedBy | Admin |
| ServiceOccurrenceDuty | OccurrenceID | ServiceOccurrence |
| ServiceOccurrenceDuty | DutyID | Duty |
| RosterAssignment | MemberID | Member |
| RosterAssignment | DutyID | Duty |
| RosterAssignment | OccurrenceID | ServiceOccurrence |
| RosterAssignment | AssignmentStatusID | AssignmentStatus |
| RosterAssignment | ApprovedBy | Admin |
| RosterAssignment | AssignedBy | Admin |
| AttendanceRecord | MemberID | Member |
| AttendanceRecord | OccurrenceID | ServiceOccurrence |
| MaterializerRun | TriggeredBy | Admin |

### Event stores

| From | Column | To |
| --- | --- | --- |
| Program | EventID | Event |
| ProgramItem | ProgramID | Program |
| ProgramItem | ServiceDefID | ServiceDefinition |
| EventDuty | ProgramItemID | ProgramItem |
| EventDuty | AssignedMemberID | Member |
| EventDuty | AssignmentStatusID | AssignmentStatus |

---

## Check constraints

### `ServiceOccurrence` — provenance consistency

| Property | Value |
| --- | --- |
| Table | ServiceOccurrence |
| Rule | GeneratedBy = 'System' → CreatedBy IS NULL |
| Rule | GeneratedBy = 'Administrator' → CreatedBy IS NOT NULL |
| Purpose | Enforces the iff relationship between provenance and creator |
| Source | §8.1.2 |

Together, these two constraints enforce: `CreatedBy IS NOT NULL ⇔ GeneratedBy = 'Administrator'`.

### `MaterializerRun` — attribution consistency

| Property | Value |
| --- | --- |
| Table | MaterializerRun |
| Rule | TriggerType = 'Manual' → TriggeredBy IS NOT NULL |
| Rule | TriggerType = 'Scheduled' → TriggeredBy IS NULL |
| Purpose | Enforces that manual runs are attributed, scheduled runs are not |
| Source | §8.1.5 |

### `MaterializerRun` — completion consistency

| Property | Value |
| --- | --- |
| Table | MaterializerRun |
| Rule | Status = 'Success' → CompletedAt IS NOT NULL |
| Rule | Status = 'Failure' → ErrorDetail IS NOT NULL |
| Purpose | Enforces that a successful run has a completion time, and a failed run has a stated reason |
| Source | §8.1.5 |

### `RosterAssignment` — no check constraint beyond uniqueness

5.0 writes RosterAssignment rows with AssignmentStatusID = NULL. 7.0 owns the lifecycle. There is no check constraint enforcing a specific status flow — the system does not enforce a state machine.

### No check constraints on other tables

The remaining tables have no check constraints beyond uniqueness, foreign keys, and nullability.

---

## Nullability clarifications

### `RosterAssignment.AssignmentStatusID` — nullable

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Column | AssignmentStatusID |
| Nullable | Yes |
| Source | §10 D4 lock, §15.4.1 |

5.0 creates assignments with AssignmentStatusID = NULL. 7.0 transitions to the initial status.

---

## What is deliberately not a constraint

The following are **not** enforced as constraints, by design:

- **MembershipStage** is free text, not a foreign key to a lookup table.
- **IdentifierHistory.Type** is free text, not a lookup.
- **Event.Type** is free text.
- **EventDuty.Label** is free text; not a reference to Duty.
- **CriteriaValue** is free text, not validated at write time.
- **Multiple active identifiers of the same Type** for one member — the system does not prevent it.
- **Status transition ordering** — no state machine is enforced.
- **Attendance window** — no constraint prevents attendance before, during, or after the occurrence.
- **Concurrent re-resolution** — the database uniqueness constraint is the correctness guard for duplicate members, but the slot-capacity invariant requires transactional control at the application level.

---

## Physical expression

How each constraint is expressed depends on the target database:

- **Filtered or partial unique indexes** for the schedule-sourced occurrence and active-schedule uniqueness rules.
- **Composite unique constraints** for the roster assignment and attendance rules.
- **Check constraints** for the provenance and materializer run rules.
- **Standard foreign keys** for all referential integrity rules.

The logical rules are fixed. The physical forms are implementation choices.

---

*Source: System Design Specification §7, §8, §9, §10, §11, §13, §15.*
