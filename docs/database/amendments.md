# Amendments

*The consolidated list of schema amendments implied by the locked operational contracts. Derived from the System Design Specification §15. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

Across the locked contracts for §7 through §14, the schema described in §4 of the System Design Specification received a small set of additions: columns, constraints, settings, and a nullability clarification. This document gathers them in one place.

The additions fall into four categories:

1. New columns
2. New constraints
3. New settings
4. Nullability clarifications

No new entities are introduced. No new tables. No new processes. Fifteen amendments total.

---

## 1. New columns

### 1.1 — RosterAssignment.CreatedAt

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Column | CreatedAt |
| Type | timestamp |
| Nullable | No |
| Set by | Every newly inserted row sets CreatedAt to its creation time. The creating process supplies it. |
| Purpose | Basis for 7.0's timeout calculation: now - CreatedAt >= ConfirmationTimeoutHours |
| Source | §11 E1 lock, §15.1.1 |

### 1.2 — AssignmentStatus.IsTerminal

| Property | Value |
| --- | --- |
| Table | AssignmentStatus |
| Column | IsTerminal |
| Type | boolean |
| Nullable | No |
| Set by | Configured by the Administrator when defining the status |
| Purpose | Distinguishes statuses that occupy a required slot from statuses that leave the slot vacant |
| Source | §11 C4 lock, §15.1.2 |

**Semantic note:** IsTerminal is a capacity concept, not a lifecycle-impermanence concept. A terminal assignment's row remains; the member may rejoin a slot through a new assignment.

---

## 2. New constraints

### 2.1 — RosterAssignment (MemberID, DutyID, OccurrenceID) unique

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Constraint | UNIQUE on (MemberID, DutyID, OccurrenceID) |
| Purpose | Prevent duplicate member/duty/occurrence records |
| Source | §10 G2 lock, §15.2.1 |

Applies regardless of AssignmentSource. The capacity rule governs how many slots are filled; the uniqueness rule governs what combinations may coexist. Both are required.

### 2.2 — AttendanceRecord (MemberID, OccurrenceID) unique

| Property | Value |
| --- | --- |
| Table | AttendanceRecord |
| Constraint | UNIQUE on (MemberID, OccurrenceID) |
| Purpose | At most one recorded attendance fact per member per occurrence |
| Source | §13 A4 lock, §15.2.2 |

A subsequent tap for an existing pair is a no-op.

### 2.3 — ServiceSchedule (ServiceDefID, TimeSlotID) active-row unique

| Property | Value |
| --- | --- |
| Table | ServiceSchedule |
| Constraint | UNIQUE on (ServiceDefID, TimeSlotID), restricted to active rows |
| Purpose | An active ServiceSchedule is uniquely identified by the pair |
| Source | §9 D1 lock, §15.2.3 |

Inactive schedules are excluded. A deactivated schedule may be replaced.

### 2.4 — ServiceOccurrence (ScheduleID, Date) schedule-sourced unique

| Property | Value |
| --- | --- |
| Table | ServiceOccurrence |
| Constraint | UNIQUE on (ScheduleID, Date), restricted to rows where ScheduleID IS NOT NULL |
| Purpose | One occurrence per schedule per date; correctness guard for 11.0 idempotency |
| Source | §7, §8, §15.2.4 |

Event-sourced occurrences are excluded. Their uniqueness is governed by the 8.0 process.

---

## 3. New settings

All settings live in SystemSetting and follow the same pattern: a fixed key name, an administrator-configured value, and a Required boolean that determines behavior when the value is absent.

### 3.1 — OccurrenceHorizonDays

| Property | Value |
| --- | --- |
| Type | integer, unit days |
| Minimum | 0 |
| Required | true |
| Consumed by | 11.0 |
| Behavior if absent | 11.0 refuses to run and surfaces a configuration error |
| Source | §7, §8, §15.3.1 |

### 3.2 — InitialAssignmentStatusID

| Property | Value |
| --- | --- |
| Type | integer — an AssignmentStatusID |
| Required | true |
| Consumed by | 7.0 |
| Behavior if absent | 7.0 refuses to run and surfaces a configuration error |
| Source | §11, §15.3.2 |

### 3.3 — ConfirmationTimeoutHours

| Property | Value |
| --- | --- |
| Type | integer, unit hours |
| Required | Configurable |
| Consumed by | 7.0 |
| Behavior if absent | If Required = false, no timeout occurs; if Required = true, 7.0 refuses to run |
| Timeout rule | now - CreatedAt >= ConfirmationTimeoutHours |
| Source | §11 E1 lock, §15.3.3 |

### 3.4 — OutcomeStateUnfilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 D2 lock, §15.3.4 |

### 3.5 — OutcomeStatePartiallyFilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 D2 lock, §15.3.5 |

### 3.6 — OutcomeStateFilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 D2 lock, §15.3.6 |

### 3.7 — OutcomeStateCancelledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | false |
| Consumed by | 10.0 |
| Behavior if absent | No automatic cancellation exclusion applies |
| Behavior if present | 10.0 skips occurrences whose FillStatusID equals that ID |
| Note | 10.0 never writes the cancelled state |
| Source | §12 E2 lock, §15.3.7 |

### 3.8 — NotificationChannel

| Property | Value |
| --- | --- |
| Type | string or enum |
| Required | Not fixed by the specification; a deployment choice |
| Consumed by | 9.0 |
| Validation | If no valid channel is configured, 9.0 does not send |
| Source | §14, §15.3.8 |

---

## 4. Nullability clarifications

### 4.1 — RosterAssignment.AssignmentStatusID nullable

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Column | AssignmentStatusID |
| Nullable | Yes |
| Source | §10 D4 lock, §15.4.1 |

5.0 creates assignments with AssignmentStatusID = NULL; 7.0 owns the lifecycle from that point.

---

## Summary table

| # | Amendment | Type | Source |
| --- | --- | --- | --- |
| 1.1 | RosterAssignment.CreatedAt | New column | §11 |
| 1.2 | AssignmentStatus.IsTerminal | New column | §11 |
| 2.1 | RosterAssignment (MemberID, DutyID, OccurrenceID) unique | New constraint | §10 |
| 2.2 | AttendanceRecord (MemberID, OccurrenceID) unique | New constraint | §13 |
| 2.3 | ServiceSchedule (ServiceDefID, TimeSlotID) active-row unique | New constraint | §9 |
| 2.4 | ServiceOccurrence (ScheduleID, Date) schedule-sourced unique | New constraint | §7, §8 |
| 3.1 | OccurrenceHorizonDays | New setting | §7, §8 |
| 3.2 | InitialAssignmentStatusID | New setting | §11 |
| 3.3 | ConfirmationTimeoutHours | New setting | §11 |
| 3.4 | OutcomeStateUnfilledID | New setting | §12 |
| 3.5 | OutcomeStatePartiallyFilledID | New setting | §12 |
| 3.6 | OutcomeStateFilledID | New setting | §12 |
| 3.7 | OutcomeStateCancelledID | New setting | §12 |
| 3.8 | NotificationChannel | New setting | §14 |
| 4.1 | RosterAssignment.AssignmentStatusID nullable | Nullability clarification | §10 |

Fifteen amendments. Two new columns, four new constraints, eight new settings, one nullability clarification.

---

## What is deliberately not added

The following are explicitly not part of the amendment set, per the pattern established throughout the specification:

- No NotificationLog or delivery-tracking table.
- No AssignmentRun or equivalent log for 5.0.
- No status-history table for RosterAssignment.
- No attendance-event table.
- No retry table.
- No orchestration entity.
- No DeactivatedBy / DeactivatedAt columns on configuration entities.
- No AttendanceSource field distinguishing manual from tap entry.
- No confirmation-token or deep-link field.
- No attendance-window engine or configuration.
- No cross-service time-conflict engine.
- No MemberRole or MemberBranch join tables.
- No reserved criteria activation.

Each is a candidate for a future additive change if a real requirement emerges. None is implied by the locked contracts.

---

## 5. Later amendments

The amendments below were added after the original fifteen. They are the changes agreed in the A–G amendment set.

### 5.1 — New entities

| Entity | Purpose | Source |
| --- | --- | --- |
| MemberStatus | Admin-defined vocabulary of member states, with an `IsRosterable` flag | §4 |
| AttributeType | Admin-defined vocabulary of member attribute names | §4 |
| MemberAttributeValue | One value per member per attribute set | §4 |
| EventBranch | Set of branches attending an event | §4 |
| Capability | Vocabulary of named actions the system can perform | §4 |
| ConfigurationAuditLog | Permanent record of every deactivation and every soft delete | §4, §9.6 |
| EntityDeletionPolicy | Per-entity-type configuration of the deletion model | §4, §9.6 |
| NotificationSubscription | Recipient configuration for authority notifications | §4, §14.1.3 |

### 5.2 — New columns

| Table | Column | Type | Nullable | Source |
| --- | --- | --- | --- | --- |
| Member | ReceiptNumber | string | Yes | §4 |
| Member | CardNumber | string | Yes | §4 |
| Member | JoinReason | string | Yes | §4 |
| Member | MemberStatusID | integer | No | §4 |
| Member | (IsActive removed) | — | — | §4 |
| Role | IsDefault | boolean | No | §4 |
| DutyRule | ServiceDefID | integer | Yes | §4, §10.1.5 |
| ProgramItem | Location | string | Yes | §4 |
| Event | HostBranchID | integer | No | §4 |
| Event | (Location removed) | — | — | §4 |
| EventDuty | DutyID | integer | No | §4 |
| EventDuty | ServiceDefID | integer | Yes | §4 |
| EventDuty | (Label removed) | — | — | §4 |
| Every configuration entity | IsDeleted | boolean | No | §4, §9.6 |

### 5.3 — Extended criteria vocabulary

| Criterion | Meaning | Source |
| --- | --- | --- |
| Member Attribute | Member holds the named Attribute Type with the required value | §5, §10.1.5 |
| Youth | Member's age falls within the configured YouthAgeMin and YouthAgeMax range | §5, §10.1.5 |

### 5.4 — New settings

| Key | Type | Required | Consumed by | Source |
| --- | --- | --- | --- | --- |
| YouthAgeMin | integer | true | 5.0 (Youth criterion) | §4, §5 |
| YouthAgeMax | integer | true | 5.0 (Youth criterion) | §4, §5 |

`ReceiptToCardDurationDays` was already defined in the original settings list.

### 5.5 — Attendance amendment set

#### 5.5.1 New entities

| Entity | Purpose | Source |
| --- | --- | --- |
| AttendanceRule | Admin-defined attendance rule with a trigger and an outcome | §4, §13.6 |
| AttendanceRuleScope | Join table of scope criteria for an Attendance Rule | §4, §13.6 |
| Readmission | One row per readmission event | §4, §13.6 |

#### 5.5.2 New columns

| Table | Column | Type | Nullable | Source |
| --- | --- | --- | --- | --- |
| AttendanceRecord | Source | string | No | §4 |
| Branch | UsesAttendanceRegister | boolean | Yes | §4, §9.6 |
| Event | UsesAttendanceRegister | boolean | Yes | §4, §9.6 |

#### 5.5.3 New process

| # | Process | Trigger | Source |
| --- | --- | --- | --- |
| 13.0 | Attendance Rule Engine | Scheduled; manual admin | §13.6, §16 |

#### 5.5.4 New invocation edge

| # | From | To | Trigger | Conditionality | Source |
| --- | --- | --- | --- | --- | --- |
| I4 | 13.0 Attendance Rule Engine | 9.0 Dispatch Notification | Attendance Rule with OutcomeType = Notify fires | Conditional | §16.2 |

The count of invoking processes is now four: 5.0, 7.0, 12.0, 13.0.

#### 5.5.5 Behaviour rules

| Rule | Statement | Source |
| --- | --- | --- |
| Attendance register scope resolution | Most specific scope wins: event, then branch, then global | §9.6 |
| Branch-Attendance Recency skip | The criterion is skipped when the effective register scope is off | §10.1.5 |
| Attendance register eligibility | A member is eligible only if they hold a ReceiptNumber or CardNumber | §4, §9.1.7 |

### 5.6 — Removed columns

| Table | Column | Reason | Source |
| --- | --- | --- | --- |
| Event | Location | Replaced by HostBranchID; finer location on Program Item | §4 |
| EventDuty | Label | Replaced by DutyID reference | §4 |
| Member | IsActive | Replaced by MemberStatusID | §4 |

## Physical expression

How each amendment is expressed in a target database depends on the engine:

- **Filtered or partial unique indexes** for the schedule-sourced occurrence and active-schedule uniqueness rules.
- **Composite unique constraints** for the roster assignment and attendance rules.
- **Standard columns and settings rows** for the rest.

The logical rules are fixed. The physical forms are implementation choices.

---

## Cross-references

- **`constraints.md`** — the full constraint list, including foreign keys and check constraints.
- **`settings.md`** — the full settings list.
- **`schema.md`** — the schema with amendments marked `(M)`.
- **Specification §15** — the authoritative amendment list.

---

*Source: System Design Specification §15.*
