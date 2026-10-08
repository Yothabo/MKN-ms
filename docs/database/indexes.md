# Indexes

*Index strategy for the system. Derived from the System Design Specification §8 through §14. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document separates three categories of index information:

1. **Required logical constraints and access patterns** — things the specification locks.
2. **Candidate indexes** — indexes that may be warranted based on the access patterns, but are not prescribed at the specification level.
3. **Physical indexes** — the indexes chosen during implementation against the target database.

The distinction is deliberate. The specification leaves some physical choices implementation-dependent. This document keeps that discipline.

---

## Category 1 — Required logical constraints

These are correctness constraints, not performance indexes. Their physical implementation — including whether a backing index is created automatically — is database-dependent.

| Table | Constraint | Source |
| --- | --- | --- |
| ServiceOccurrence | UNIQUE (ScheduleID, Date), schedule-sourced rows | §7, §8, §15.2.4 |
| ServiceSchedule | UNIQUE (ServiceDefID, TimeSlotID), rows where IsActive = true AND IsDeleted = false | §9.1.5, §15.2.3 |
| RosterAssignment | UNIQUE (MemberID, DutyID, OccurrenceID) | §10.4.1, §15.2.1 |
| AttendanceRecord | UNIQUE (MemberID, OccurrenceID) | §13.4.1, §15.2.2 |

Each of these is required as a rule. Whether it creates a backing index, and how, depends on the target database and on how the constraint is expressed.

---

## Category 2 — Candidate indexes

These are indexes that may be warranted based on the access patterns the processes require, but are not prescribed at the specification level. Whether each is created depends on the target database and observed query plans.

### Serves 11.0 Materialize Occurrences

- **ServiceSchedule (IsActive), active rows only.** Serves the query for active schedules.
- The ServiceOccurrence uniqueness constraint (ScheduleID, Date) already serves the existence check.

### Serves 5.0 Generate Assignment

- **RosterAssignment (OccurrenceID, DutyID).** Serves the capacity count and availability check. Hot path for filling slots.
- **Eligibility (MemberID, DutyID).** Serves eligibility resolution.
- **DutyRule (DutyID, TierOrder).** Serves tier evaluation.

### Serves 7.0 Manage Confirmation

- **RosterAssignment (CreatedAt).** Serves the scheduled timeout check.
- **RosterAssignment (OccurrenceID, DutyID, AssignmentStatusID).** Serves affected-slot resolution during re-resolution. The existing (OccurrenceID, DutyID) index from 5.0 covers the first two columns; whether a third column is warranted is a query-plan decision.

### Serves 10.0 Evaluate Fill Status

- **RosterAssignment (OccurrenceID, DutyID).** Already noted for 5.0; serves 10.0's fill-state computation as well.
- **ServiceOccurrence (FillStatusID).** Serves "which occurrences are unfilled" queries from administrator dashboards. Whether warranted depends on whether such queries are common.

### Serves 13.0 Attendance Rule Engine

- **AttendanceRule (Enabled).** Serves the query for the enabled rule set.
- **AttendanceRuleScope (AttendanceRuleID).** Serves rule scope resolution.
- **Readmission (MemberID, IsDeleted).** Serves the readmission count.

### Serves 6.0 Record Attendance

- **AttendanceRecord (OccurrenceID).** Natural access pattern for "who attended this occurrence" queries.
- **AttendanceRecord (MemberID, Timestamp).** Natural access pattern for Branch-Attendance Recency evaluation by 5.0 and for member-facing attendance history.

### Serves 9.0 Dispatch Notification

None. 9.0 reads by primary key from RosterAssignment, Member, and ServiceOccurrence, and by key from SystemSetting. All are served by existing primary-key or foreign-key indexes.

### Serves configuration reads

- **Foreign-key indexes** on every FK column, standard. These serve joins throughout the configuration layer.

---

## Category 3 — Physical indexes

The physical indexes actually chosen during implementation. These depend on:

- **Target database engine.** Whether filtered or partial indexes are supported; whether NULL-distinct behavior is available; whether the planner benefits from composite forms.
- **Observed query plans.** Whether a candidate index is actually used; whether a different shape would be better.
- **Data volume.** At small scale, some candidate indexes are not worth their maintenance cost. At larger scale, they become necessary.

Physical indexes are chosen during implementation, against the actual load and query patterns. They are not prescribed by the specification.

---

## Notes on specific shapes

### Filtered and partial unique constraints

The ServiceOccurrence (ScheduleID, Date) and ServiceSchedule (ServiceDefID, TimeSlotID) uniqueness rules apply only to a subset of rows:

- ServiceOccurrence: only rows where ScheduleID IS NOT NULL.
- ServiceSchedule: only rows where IsActive = true AND IsDeleted = false.

How this is expressed depends on the target database. The logical rule is fixed. The physical form is a target-database choice.

### Composite index ordering

Where a composite index is a candidate, the column order matters:

- **RosterAssignment (OccurrenceID, DutyID)** — OccurrenceID first because it is the grouping key; DutyID second because it narrows within the group.
- **RosterAssignment (OccurrenceID, DutyID, AssignmentStatusID)** — the third column's inclusion depends on whether the status filter is applied in the same query or a separate one.
- **Eligibility (MemberID, DutyID)** — MemberID first because it is typically the more selective predicate.
- **DutyRule (DutyID, TierOrder)** — DutyID first because reads are always per-duty; TierOrder second because it serves the ordering.

### Deliberately not indexed

The following are not candidates for indexing at the specification level:

- **Role.Name, Duty.Name, Branch.Name** — no uniqueness, no name-based lookups in the module contracts.
- **RosterAssignment (AssignmentSource)** — no query pattern filters on source.
- **RosterAssignment (AssignmentStatusID) as a standalone index** — read as part of per-occurrence queries; a standalone index is not warranted.
- **AttendanceRecord (Timestamp) standalone** — read in the context of a member or occurrence.
- **ServiceOccurrence (GeneratedBy)** — no query filters by provenance.
- **ServiceOccurrence (EventID)** — event-sourced occurrences are outside 11.0's scope; if 8.0 needs it, that is 8.0's concern.

---

## Summary

**Required (Category 1):** four uniqueness constraints. Each is a rule; physical enforcement is database-dependent.

**Candidate (Category 2):** approximately ten indexes across the operational and configuration tables, each tied to a specific access pattern.

**Physical (Category 3):** chosen during implementation against the target database.

The specification does not prescribe physical indexes. It names the access patterns each process requires, and leaves the physical form to implementation.

---

## Cross-references

- **`../database/constraints.md`** — the uniqueness constraints.
- **`../processes/operations/11.0-materialize-occurrences.md`** — the access patterns for 11.0.
- **`../processes/operations/5.0-generate-assignment.md`** — the access patterns for 5.0.
- **`../processes/operations/7.0-manage-confirmation.md`** — the access patterns for 7.0.
- **`../processes/operations/10.0-evaluate-fill-status.md`** — the access patterns for 10.0.
- **`../processes/operations/6.0-record-attendance.md`** — the access patterns for 6.0.

---

*Source: System Design Specification §8.4, §9.4, §10.4, §11.4, §12.4, §13.4, §14.4.*
