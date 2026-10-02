# Entity Reference

*Every table in the schema, every column, its type, and its notes. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## How to read this document

Each entity is presented as a table with columns, types, and notes. Nullable columns are marked. Foreign keys are noted with their target. Constraint references point to `../database/constraints.md` where the full constraint list lives.

Types are stated generically — integer, string, boolean, timestamp, date, time, decimal. Physical types are an implementation choice (see `../database/schema.md`).

---

## D1 — Role / Duty

### Role

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RoleID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

### Duty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| DutyID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

Duty carries no reference to Role. The relationship is expressed via Duty Rule.

---

## D2 — Duty Rule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RuleID | integer | No | Primary key |
| DutyID | integer | No | Foreign key → Duty |
| TierOrder | integer | No | Administrator-defined sequence position |
| CriteriaType | string | No | One of the supported criteria types |
| CriteriaValue | string | No | Administrator-entered value, interpreted per CriteriaType |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

Multiple rules may share the same (DutyID, TierOrder). Same-tier rules are ANDed. Gaps in TierOrder are permitted.

---

## D3 — Branch / Time Slot / Service / Occurrence

### Branch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| BranchID | integer | No | Primary key |
| Name | string | No | Administrator-defined label |
| Location | structured | No | Single entry, structured internally |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

### BranchTimeSlot

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeSlotID | integer | No | Primary key |
| BranchID | integer | No | Foreign key → Branch |
| DayOfWeek | string | No |  |
| TimeOfDayID | integer | No | Foreign key → TimeOfDay |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

Multiple slots per (BranchID, DayOfWeek) are permitted. StartTime does not live here.

### ServiceDefinition

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Primary key |
| Name | string | No | Administrator-assigned label |
| ServiceTypeID | integer | No | Foreign key → ServiceType |
| OwningBranchID | integer | Yes | Null = shared across branches; set = exclusive to one |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

### ServiceDefinitionDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition |
| DutyID | integer | No | Foreign key → Duty |
| RequiredSlotCount | integer | No | Administrator-set. No default. |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

### ServiceSchedule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ScheduleID | integer | No | Primary key |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition |
| TimeSlotID | integer | No | Foreign key → BranchTimeSlot |
| StartTime | time | No | Concrete clock time |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

Unique on (ServiceDefID, TimeSlotID) for active rows.

### ServiceOccurrence

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Primary key |
| ScheduleID | integer | Yes | Foreign key → ServiceSchedule. Set for schedule-sourced occurrences. |
| EventID | integer | Yes | Foreign key → Event. Set for event-sourced occurrences. |
| Date | date | No | Date-only; start time represented separately |
| ServiceTypeID (override) | integer | Yes | Foreign key → ServiceType. Inherited from service definition; overridable. |
| StartTime (override) | time | Yes | Inherited from schedule; overridable. |
| FillStatusID | integer | Yes | Foreign key → OutcomeState. Null at creation. |
| GeneratedBy | enum | No | System or Administrator. Permanent provenance. |
| CreatedBy | integer | Yes | Foreign key → Admin. Populated only when GeneratedBy = Administrator. |
| ChangedBy | integer | Yes | Foreign key → Admin. Null at creation. |

Unique on (ScheduleID, Date) for schedule-sourced rows (ScheduleID NOT NULL).

### ServiceOccurrenceDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| DutyID | integer | No | Foreign key → Duty |
| Action | enum | No | Added or Removed |
| RequiredSlotCount (override) | integer | Yes | Overrides the service definition's slot count for this date only |

Only occurrences with an actual override need a row here.

---

## D4 — Member

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberID | integer | No | Primary key |
| JoinDate | date | No |  |
| DateOfBirth | date | No |  |
| MembershipStage | string | No | Administrator-defined |
| Name | string | No |  |
| Surname | string | No |  |
| Gender | string | No |  |
| Phone | string | No |  |
| Email | string | Yes | Optional |
| BranchID | integer | No | Foreign key → Branch |
| RoleID | integer | No | Foreign key → Role |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

---

## D5 — Identifier History

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EntryID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| Type | string | No | Administrator-defined identifier category |
| Number | string | No | Administrator-supplied |
| AssignedDate | date | No |  |
| UnassignedDate | date | Yes | Optional. Set on retirement. |
| Reason | string | Yes | Optional |
| AuthorizedBy | integer | No | Foreign key → Admin |

Identifier History has no IsActive flag. Entries are added and retired via UnassignedDate; they are never deleted.

---

## D6 — Eligibility

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EligibilityID | integer | No | Primary key — durable identifier |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| GrantedDate | date | No |  |
| GrantedBy | integer | No | Foreign key → Admin |
| RevokedDate | date | Yes | Optional. Set on revocation. |
| RevokedReason | string | Yes | Optional |

Each grant/revoke cycle is its own row. Current eligibility is determined by the applicable grant/revoke record.

---

## D7 — Roster Assignment

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| AssignmentStatusID | integer | Yes | Foreign key → AssignmentStatus. Null at automatic creation. |
| ApprovedBy | integer | Yes | Foreign key → Admin |
| AssignmentSource | enum | No | Automatic or Manual |
| AssignedBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| CreatedAt | timestamp | No | Basis for confirmation timeout |

Unique on (MemberID, DutyID, OccurrenceID).

---

## D8 — Attendance Record

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RecordID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| Timestamp | timestamp | No |  |

Unique on (MemberID, OccurrenceID).

---

## D9 — Event / Program / Program Item / Event Duty

### Event

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Primary key |
| Name | string | No |  |
| StartDate | date | No |  |
| EndDate | date | No |  |
| Location | string | No |  |
| Type | string | No | Free text |
| IsActive | boolean | No | True at creation. Deactivated, never deleted. |

### Program

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ProgramID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event. One program per event. |
| Title | string | No |  |

### ProgramItem

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ItemID | integer | No | Primary key |
| ProgramID | integer | No | Foreign key → Program |
| SequenceOrder | integer | No |  |
| Title | string | No |  |
| ScheduledStart | timestamp | No |  |
| ScheduledEnd | timestamp | No |  |
| ServiceDefID | integer | Yes | Foreign key → ServiceDefinition. Optional. |

Status is not stored. It is computed from ScheduledStart/ScheduledEnd and the current time.

### EventDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventDutyID | integer | No | Primary key |
| ProgramItemID | integer | No | Foreign key → ProgramItem |
| Label | string | No | Free text, admin-typed, no preset list |
| AssignedMemberID | integer | No | Foreign key → Member. Direct pick; no eligibility or priority run. |
| AssignmentStatusID | integer | Yes | Foreign key → AssignmentStatus. Optional. |

---

## D10 — Config Lookups

### OutcomeState

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OutcomeStateID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |

### AssignmentStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |
| IsTerminal | boolean | No | Distinguishes slot-occupying from vacant statuses |

### PermissionTier

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PermissionTierID | integer | No | Primary key |
| Name | string | No | Administrator-defined |

### TimeOfDay

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeOfDayID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |

### ServiceType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |

---

## D11 — System Setting

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| Key | string | No | Primary key — administrator-facing setting name |
| Value | string | Yes | The configured value |
| Required | boolean | No | If true, absence causes consumer to refuse to run |
| Description | string | Yes | Optional, administrator-facing explanation |

---

## D12 — Materializer Run

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RunID | integer | No | Primary key |
| TriggerType | enum | No | Scheduled or Manual |
| TriggeredBy | integer | Yes | Foreign key → Admin. Populated only when TriggerType = Manual. |
| StartedAt | timestamp | No |  |
| CompletedAt | timestamp | Yes | Null if the run failed before completing |
| SchedulesEvaluated | integer | No | Count |
| OccurrencesCreated | integer | No | Count |
| Status | enum | No | Success or Failure |
| ErrorDetail | string | Yes | Populated on failure |

---

## D-Admin

### Admin

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AdminID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| PermissionTierID | integer | No | Foreign key → PermissionTier |

---

## Summary of foreign keys

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
| ServiceOccurrence | ScheduleID | ServiceSchedule |
| ServiceOccurrence | EventID | Event |
| ServiceOccurrence | FillStatusID | OutcomeState |
| ServiceOccurrence | CreatedBy | Admin |
| ServiceOccurrence | ChangedBy | Admin |
| ServiceOccurrenceDuty | OccurrenceID | ServiceOccurrence |
| ServiceOccurrenceDuty | DutyID | Duty |
| Member | BranchID | Branch |
| Member | RoleID | Role |
| IdentifierHistory | MemberID | Member |
| IdentifierHistory | AuthorizedBy | Admin |
| Eligibility | MemberID | Member |
| Eligibility | DutyID | Duty |
| Eligibility | GrantedBy | Admin |
| RosterAssignment | MemberID | Member |
| RosterAssignment | DutyID | Duty |
| RosterAssignment | OccurrenceID | ServiceOccurrence |
| RosterAssignment | AssignmentStatusID | AssignmentStatus |
| RosterAssignment | ApprovedBy | Admin |
| RosterAssignment | AssignedBy | Admin |
| AttendanceRecord | MemberID | Member |
| AttendanceRecord | OccurrenceID | ServiceOccurrence |
| Program | EventID | Event |
| ProgramItem | ProgramID | Program |
| ProgramItem | ServiceDefID | ServiceDefinition |
| EventDuty | ProgramItemID | ProgramItem |
| EventDuty | AssignedMemberID | Member |
| EventDuty | AssignmentStatusID | AssignmentStatus |
| Admin | MemberID | Member |
| Admin | PermissionTierID | PermissionTier |
| MaterializerRun | TriggeredBy | Admin |

---

*Source: System Design Specification §4, §15.*
