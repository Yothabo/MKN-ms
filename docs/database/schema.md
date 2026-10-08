# Schema

*Every table, column, type, and nullability in the logical data model. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## How to read this document

Each entity is presented as a table with columns, types, and notes. Nullable columns are marked. Foreign keys are noted with their target.

Types are stated generically — integer, string, boolean, timestamp, date, time. Physical types are an implementation choice.

Configuration entities carry both `IsActive` and `IsDeleted`. Operational entities carry neither. The two-flag lifecycle is stated in Specification §9.6.

---

## Role

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RoleID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsDefault | boolean | No | Exactly one Role is the default. |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## Duty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| DutyID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## DutyRule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RuleID | integer | No | Primary key |
| DutyID | integer | No | Foreign key → Duty |
| ServiceDefID | integer | Yes | Foreign key → ServiceDefinition. Null means the rule applies to the Duty wherever it appears. Set means the rule applies only where the Duty is required by that service. |
| TierOrder | integer | No | Administrator-defined sequence position |
| CriteriaType | string | No | One of the supported criteria types |
| CriteriaValue | string | No | Interpreted per CriteriaType |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## Branch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| BranchID | integer | No | Primary key |
| Name | string | No |  |
| Location | structured | No | Single entry, structured internally |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## BranchTimeSlot

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeSlotID | integer | No | Primary key |
| BranchID | integer | No | Foreign key → Branch |
| DayOfWeek | string | No |  |
| TimeOfDayID | integer | No | Foreign key → TimeOfDay |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## ServiceType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## TimeOfDay

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeOfDayID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## ServiceDefinition

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Primary key |
| Name | string | No | Administrator-assigned |
| ServiceTypeID | integer | No | Foreign key → ServiceType |
| OwningBranchID | integer | Yes | Foreign key → Branch. Null = shared; set = exclusive. |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## ServiceDefinitionDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition. Part of composite primary key. |
| DutyID | integer | No | Foreign key → Duty. Part of composite primary key. |
| RequiredSlotCount | integer | No | Administrator-set. No default. |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## ServiceSchedule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ScheduleID | integer | No | Primary key |
| ServiceDefID | integer | No | Foreign key → ServiceDefinition |
| TimeSlotID | integer | No | Foreign key → BranchTimeSlot |
| StartTime | time | No | Concrete clock time |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

Unique on (ServiceDefID, TimeSlotID) for active, not-deleted rows.

## ServiceOccurrence

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Primary key |
| ScheduleID | integer | Yes | Foreign key → ServiceSchedule. Present for schedule-sourced. |
| EventID | integer | Yes | Foreign key → Event. Present for event-sourced. |
| Date | date | No | Date-only |
| ServiceTypeID | integer | Yes | Foreign key → ServiceType |
| StartTime | time | Yes |  |
| FillStatusID | integer | Yes | Foreign key → OutcomeState. Null at creation. |
| GeneratedBy | string | No | System or Administrator. Permanent. |
| CreatedBy | integer | Yes | Foreign key → Admin. Populated only when GeneratedBy = Administrator. |
| ChangedBy | integer | Yes | Foreign key → Admin. Null at creation. |

Unique on (ScheduleID, Date) for schedule-sourced rows. Operational entity; no lifecycle flags.

## ServiceOccurrenceDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence. Part of composite primary key. |
| DutyID | integer | No | Foreign key → Duty. Part of composite primary key. |
| Action | string | No | Added or Removed |
| RequiredSlotCount | integer | Yes |  |

Operational entity; no lifecycle flags.

## MemberStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsRosterable | boolean | No | Whether a member with this status may be rostered |
| Description | string | Yes |  |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## Member

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberID | integer | No | Primary key |
| ReceiptNumber | string | Yes | Congregational identifier held for the first configured duration |
| CardNumber | string | Yes | Congregational identifier issued after the configured duration. Permanent. |
| JoinDate | date | No |  |
| JoinReason | string | Yes | Free text |
| DateOfBirth | date | No |  |
| MembershipStage | string | No | Administrator-defined |
| Name | string | No |  |
| Surname | string | No |  |
| Gender | string | No |  |
| Phone | string | No |  |
| Email | string | Yes | Optional |
| BranchID | integer | No | Foreign key → Branch |
| RoleID | integer | No | Foreign key → Role |
| MemberStatusID | integer | No | Foreign key → MemberStatus |

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

No lifecycle flags. Entries are retired via UnassignedDate, never deleted.

## Eligibility

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EligibilityID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| GrantedDate | date | No |  |
| GrantedBy | integer | No | Foreign key → Admin |
| RevokedDate | date | Yes | Optional |
| RevokedReason | string | Yes | Optional |

No lifecycle flags.

## RosterAssignment

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| OccurrenceID | integer | No | Foreign key → ServiceOccurrence |
| AssignmentStatusID | integer | Yes | Foreign key → AssignmentStatus |
| ApprovedBy | integer | Yes | Foreign key → Admin |
| AssignmentSource | string | No | Automatic or Manual |
| AssignedBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| CreatedAt | timestamp | No | Basis for confirmation timeout |

Unique on (MemberID, DutyID, OccurrenceID). Operational entity; no lifecycle flags.

## OutcomeState

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OutcomeStateID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## AssignmentStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsTerminal | boolean | No | Distinguishes slot-occupying from vacant statuses |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## PermissionTier

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PermissionTierID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

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

Unique on (MemberID, OccurrenceID). Operational entity; no lifecycle flags.

## Event

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Primary key |
| Name | string | No |  |
| StartDate | date | No |  |
| EndDate | date | No |  |
| Type | string | No | Free text |
| HostBranchID | integer | No | Foreign key → Branch |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## EventBranch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Foreign key → Event. Part of composite primary key. |
| BranchID | integer | No | Foreign key → Branch. Part of composite primary key. |

No lifecycle flags.

## Program

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ProgramID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event. One program per event. |
| Title | string | No |  |
| IsDeleted | boolean | No | False at creation |

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
| Location | string | Yes | Finer location than the branch |
| IsDeleted | boolean | No | False at creation |

No stored status. State is computed from scheduled times.

## EventDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventDutyID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event |
| DutyID | integer | No | Foreign key → Duty |
| ServiceDefID | integer | Yes | Foreign key → ServiceDefinition. Optional scope. |
| RequiredSlotCount | integer | No |  |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## AttributeType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AttributeTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined attribute name |
| Description | string | Yes |  |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## MemberAttributeValue

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberAttributeValueID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| AttributeTypeID | integer | No | Foreign key → AttributeType |
| Value | string | No | Administrator-supplied |
| RecordedDate | date | No |  |
| RecordedBy | integer | No | Foreign key → Admin |

Unique on (MemberID, AttributeTypeID). Operational fact; no lifecycle flags.

## Capability

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| CapabilityID | integer | No | Primary key |
| Name | string | No | Administrator-defined action name |
| Description | string | Yes |  |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## ConfigurationAuditLog

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AuditID | integer | No | Primary key |
| EntityType | string | No | CLR type name of the affected entity |
| EntityID | integer | No | Primary key of the affected row |
| EntityName | string | No | Name at the time of action |
| Action | string | No | Deactivate or SoftDelete |
| Reason | string | Yes | Required for SoftDelete; optional for Deactivate |
| InitiatedByAdminID | integer | No | Foreign key → Admin |
| ApprovedByAdminID | integer | No | Foreign key → Admin |
| InitiatedAt | timestamp | No |  |
| ApprovedAt | timestamp | No |  |
| ConsequencesPreviewed | string | No |  |
| ConsequencesOccurred | string | No |  |
| NotifiedAt | timestamp | Yes | Set once notified |

No lifecycle flags. Audit records are permanent.

## EntityDeletionPolicy

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PolicyID | integer | No | Primary key |
| EntityType | string | No | Unique |
| RequiresApprovalForDelete | boolean | No |  |
| RequiresApprovalForDeactivate | boolean | No |  |
| RequiredDeletePermissionTierID | integer | Yes | Foreign key → PermissionTier |
| RequiredDeactivatePermissionTierID | integer | Yes | Foreign key → PermissionTier |
| RequiresReasonForDelete | boolean | No |  |
| RequiresReasonForDeactivate | boolean | No |  |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

## NotificationSubscription

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| SubscriptionID | integer | No | Primary key |
| EventType | string | No |  |
| RecipientTierID | integer | Yes | Foreign key → PermissionTier |
| RecipientAdminID | integer | Yes | Foreign key → Admin |
| IsActive | boolean | No | True at creation |
| IsDeleted | boolean | No | False at creation |

Exactly one of RecipientTierID or RecipientAdminID is set per row.

## SystemSetting

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| Key | string | No | Primary key |
| Value | string | Yes | Configured value |
| Required | boolean | No |  |
| Description | string | Yes | Optional |

No lifecycle flags.

## MaterializerRun

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RunID | integer | No | Primary key |
| TriggerType | string | No | Scheduled or Manual |
| TriggeredBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| StartedAt | timestamp | No |  |
| CompletedAt | timestamp | Yes |  |
| SchedulesEvaluated | integer | No | Count |
| OccurrencesCreated | integer | No | Count |
| Status | string | No | Success or Failure |
| ErrorDetail | string | Yes |  |

No lifecycle flags.

---

## Summary

| Store | Tables |
| --- | --- |
| D1 | Role, Duty |
| D2 | DutyRule |
| D3 | Branch, BranchTimeSlot, ServiceDefinition, ServiceDefinitionDuty, ServiceSchedule, ServiceOccurrence, ServiceOccurrenceDuty |
| D4 | Member, MemberStatus, AttributeType, MemberAttributeValue, Admin |
| D5 | IdentifierHistory |
| D6 | Eligibility |
| D7 | RosterAssignment |
| D8 | AttendanceRecord |
| D9 | Event, EventBranch, Program, ProgramItem, EventDuty |
| D10 | OutcomeState, AssignmentStatus, PermissionTier, TimeOfDay, ServiceType, Capability |
| D11 | SystemSetting |
| D12 | MaterializerRun |

A configuration-lifecycle store holds ConfigurationAuditLog, EntityDeletionPolicy, and NotificationSubscription. These record the two-flag lifecycle and its notification fan-out.

---

*Source: System Design Specification §4, §15.*
