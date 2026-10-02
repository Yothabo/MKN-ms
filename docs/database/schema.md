# Schema

*Every table, column, type, and nullability in the logical data model. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## How to read this document

Each entity is presented as a table with columns, types, and notes. Nullable columns are marked. Foreign keys are noted with their target.

Types are stated generically — integer, string, boolean, timestamp, date, time. Physical types are an implementation choice.

The `(M)` marker indicates an amendment from §15 — a column or constraint added by the locked operational contracts.

---

## Role

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RoleID | integer | No | Primary key |
| Name | string | No | Administrator-defined. Not unique. |
| IsActive | boolean | No | True at creation |

## Duty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| DutyID | integer | No | Primary key |
| Name | string | No | Administrator-defined. Not unique. |
| IsActive | boolean | No | True at creation |

## DutyRule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RuleID | integer | No | Primary key |
| DutyID | integer | No | Foreign key → Duty |
| TierOrder | integer | No | Administrator-defined sequence position |
| CriteriaType | string | No | One of the supported criteria types |
| CriteriaValue | string | No | Interpreted per CriteriaType |
| IsActive | boolean | No | True at creation |

## Branch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| BranchID | integer | No | Primary key |
| Name | string | No |  |
| Location | structured | No | Single entry, structured internally |
| IsActive | boolean | No | True at creation |

## BranchTimeSlot

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeSlotID | integer | No | Primary key |
| BranchID | integer | No | Foreign key → Branch |
| DayOfWeek | string | No |  |
| TimeOfDayID | integer | No | Foreign key → TimeOfDay |
| IsActive | boolean | No | True at creation |

## ServiceType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined |

## TimeOfDay

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeOfDayID | integer | No | Primary key |
| Name | string | No | Administrator-defined |

## ServiceDefinition

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Primary key |
| Name | string | No | Administrator-assigned |
| ServiceTypeID | integer | No | Foreign key → ServiceType |
| OwningBranchID | integer | Yes | Null = shared; set = exclusive |
| IsActive | boolean | No | True at creation |

## ServiceDefinitionDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition |
| DutyID | integer | No | Foreign key → Duty |
| RequiredSlotCount | integer | No | Administrator-set. No default. |
| IsActive | boolean | No | True at creation |

## ServiceSchedule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ScheduleID | integer | No | Primary key |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition |
| TimeSlotID | integer | No | Foreign key → BranchTimeSlot |
| StartTime | time | No | Concrete clock time |
| IsActive | boolean | No | True at creation |

Unique on (ServiceDefID, TimeSlotID) for active rows. `(M)`

## ServiceOccurrence

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Primary key |
| ScheduleID | integer | Yes | Foreign key → ServiceSchedule. Set for schedule-sourced. |
| EventID | integer | Yes | Foreign key → Event. Set for event-sourced. |
| Date | date | No | Date-only |
| ServiceTypeID (override) | integer | Yes | Foreign key → ServiceType |
| StartTime (override) | time | Yes |  |
| FillStatusID | integer | Yes | Foreign key → OutcomeState. Null at creation. |
| GeneratedBy | enum | No | System or Administrator. Permanent. |
| CreatedBy | integer | Yes | Foreign key → Admin. Populated only when GeneratedBy = Administrator. |
| ChangedBy | integer | Yes | Foreign key → Admin. Null at creation. |

Unique on (ScheduleID, Date) for schedule-sourced rows. `(M)`

## ServiceOccurrenceDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| DutyID | integer | No | Foreign key → Duty |
| Action | enum | No | Added or Removed |
| RequiredSlotCount (override) | integer | Yes |  |

## Role / Duty — see above

## Member

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
| IsActive | boolean | No | True at creation |

## IdentifierHistory

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EntryID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| Type | string | No | Administrator-defined category |
| Number | string | No | Administrator-supplied |
| AssignedDate | date | No |  |
| UnassignedDate | date | Yes | Optional |
| Reason | string | Yes | Optional |
| AuthorizedBy | integer | No | Foreign key → Admin |

No IsActive flag. Entries are retired via UnassignedDate, never deleted.

## Eligibility

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EligibilityID | integer | No | Primary key — durable |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| GrantedDate | date | No |  |
| GrantedBy | integer | No | Foreign key → Admin |
| RevokedDate | date | Yes | Optional |
| RevokedReason | string | Yes | Optional |

## RosterAssignment

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| AssignmentStatusID | integer | Yes | Foreign key → AssignmentStatus. Null at automatic creation. `(M)` |
| ApprovedBy | integer | Yes | Foreign key → Admin |
| AssignmentSource | enum | No | Automatic or Manual |
| AssignedBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| CreatedAt | timestamp | No | Basis for confirmation timeout. `(M)` |

Unique on (MemberID, DutyID, OccurrenceID). `(M)`

## OutcomeState

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OutcomeStateID | integer | No | Primary key |
| Name | string | No | Administrator-defined |

## AssignmentStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsTerminal | boolean | No | Distinguishes slot-occupying from vacant statuses. `(M)` |

## PermissionTier

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PermissionTierID | integer | No | Primary key |
| Name | string | No | Administrator-defined |

## Admin

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AdminID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| PermissionTierID | integer | No | Foreign key → PermissionTier |

## AttendanceRecord

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RecordID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| Timestamp | timestamp | No |  |

Unique on (MemberID, OccurrenceID). `(M)`

## SystemSetting

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| Key | string | No | Primary key |
| Value | string | Yes | Configured value |
| Required | boolean | No | If true, absence causes consumer to refuse |
| Description | string | Yes | Optional |

## MaterializerRun

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RunID | integer | No | Primary key |
| TriggerType | enum | No | Scheduled or Manual |
| TriggeredBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| StartedAt | timestamp | No |  |
| CompletedAt | timestamp | Yes | Null if failed before completing |
| SchedulesEvaluated | integer | No | Count |
| OccurrencesCreated | integer | No | Count |
| Status | enum | No | Success or Failure |
| ErrorDetail | string | Yes | Populated on failure |

## Event

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Primary key |
| Name | string | No |  |
| StartDate | date | No |  |
| EndDate | date | No |  |
| Location | string | No | Free text |
| Type | string | No | Free text |
| IsActive | boolean | No | True at creation |

## Program

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ProgramID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event. One program per event. |
| Title | string | No |  |

## ProgramItem

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ItemID | integer | No | Primary key |
| ProgramID | integer | No | Foreign key → Program |
| SequenceOrder | integer | No |  |
| Title | string | No |  |
| ScheduledStart | timestamp | No |  |
| ScheduledEnd | timestamp | No |  |
| ServiceDefID | integer | Yes | Foreign key → ServiceDefinition. Optional. |

No stored status. State is computed from scheduled times.

## EventDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventDutyID | integer | No | Primary key |
| ProgramItemID | integer | No | Foreign key → ProgramItem |
| Label | string | No | Free text, no preset list |
| AssignedMemberID | integer | No | Foreign key → Member. Direct pick. |
| AssignmentStatusID | integer | Yes | Foreign key → AssignmentStatus. Optional. |

---

## Summary

| Store | Tables |
| --- | --- |
| D1 | Role, Duty |
| D2 | DutyRule |
| D3 | Branch, BranchTimeSlot, ServiceDefinition, ServiceDefinitionDuty, ServiceSchedule, ServiceOccurrence, ServiceOccurrenceDuty |
| D4 | Member |
| D5 | IdentifierHistory |
| D6 | Eligibility |
| D7 | RosterAssignment |
| D8 | AttendanceRecord |
| D9 | Event, Program, ProgramItem, EventDuty |
| D10 | OutcomeState, AssignmentStatus, PermissionTier, TimeOfDay, ServiceType |
| D11 | SystemSetting |
| D12 | MaterializerRun |

---

*Source: System Design Specification §4, §15.*
