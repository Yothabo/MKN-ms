# Entity Reference

*Every table in the schema, every column, its type, and its notes. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## How to read this document

Each entity is presented as a table with columns, types, and notes. Nullable columns are marked. Foreign keys are noted with their target. Constraint references point to `../database/constraints.md` where the full constraint list lives.

Types are stated generically — integer, string, boolean, timestamp, date, time. Physical types are an implementation choice.

Configuration entities carry both `IsActive` and `IsDeleted`. Operational entities carry neither. The two-flag lifecycle is stated in Specification §9.6.

---

## D1 — Role / Duty

### Role

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RoleID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsDefault | boolean | No | Exactly one Role is the default. The default Role cannot be soft-deleted. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### Duty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| DutyID | integer | No | Primary key |
| Name | string | No | Administrator-defined label. Not unique. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Duty carries no reference to Role. The relationship, when one exists, is expressed via Duty Rule.

---

## D2 — Duty Rule

### DutyRule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RuleID | integer | No | Primary key |
| DutyID | integer | No | Foreign key → Duty |
| ServiceDefID | integer | Yes | Foreign key → Service Definition. Null means the rule applies to the Duty wherever it appears. Set means the rule applies only where the Duty is required by that Service Definition. |
| TierOrder | integer | No | Administrator-defined sequence position |
| CriteriaType | string | No | One of the supported criteria types |
| CriteriaValue | string | No | Interpreted per CriteriaType at evaluation time |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Per-service rules override duty-global rules entirely for that (Service, Duty) pair. Same-tier rules are ANDed.

---

## D3 — Branch / Time Slot / Service / Occurrence

### Branch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| BranchID | integer | No | Primary key |
| Name | string | No | Administrator-defined label |
| Location | structured | No | Single entry, structured internally |
| UsesAttendanceRegister | boolean | Yes | Null means no override at this scope; the effective state follows the global setting. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### BranchTimeSlot

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeSlotID | integer | No | Primary key |
| BranchID | integer | No | Foreign key → Branch |
| DayOfWeek | string | No |  |
| TimeOfDayID | integer | No | Foreign key → TimeOfDay |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Multiple slots per (BranchID, DayOfWeek) are permitted. StartTime does not live here.

### ServiceDefinition

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Primary key |
| Name | string | No | Administrator-assigned label |
| ServiceTypeID | integer | No | Foreign key → Service Type |
| OwningBranchID | integer | Yes | Foreign key → Branch. Null means available to any branch; set means exclusive to one. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### ServiceDefinitionDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceDefID | integer | No | Foreign key → Service Definition. Part of composite primary key. |
| DutyID | integer | No | Foreign key → Duty. Part of composite primary key. |
| RequiredSlotCount | integer | No | Administrator-set. No default. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### ServiceSchedule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ScheduleID | integer | No | Primary key |
| ServiceDefID | integer | No | Foreign key → Service Definition |
| TimeSlotID | integer | No | Foreign key → Branch Time Slot |
| StartTime | time | No | Concrete clock time |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Active-and-not-deleted uniqueness on (ServiceDefID, TimeSlotID).

### ServiceOccurrence

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Primary key |
| ScheduleID | integer | Yes | Foreign key → Service Schedule. Present for schedule-sourced occurrences. |
| EventID | integer | Yes | Foreign key → Event. Present for event-sourced occurrences. |
| Date | date | No | Date-only. Start time represented separately. |
| ServiceTypeID | integer | Yes | Foreign key → Service Type. Override. |
| StartTime | time | Yes | Override. |
| FillStatusID | integer | Yes | Foreign key → Outcome State. Null at creation. |
| GeneratedBy | string | No | System or Administrator. Permanent provenance. |
| CreatedBy | integer | Yes | Foreign key → Admin. Populated only when GeneratedBy = Administrator. |
| ChangedBy | integer | Yes | Foreign key → Admin. |

Operational entity. No `IsActive` and no `IsDeleted`.

### ServiceOccurrenceDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OccurrenceID | integer | No | Foreign key → Service Occurrence. Part of composite primary key. |
| DutyID | integer | No | Foreign key → Duty. Part of composite primary key. |
| Action | string | No | Added or Removed |
| RequiredSlotCount | integer | Yes | Override for this date only |

Operational entity. No `IsActive` and no `IsDeleted`.

---

## D4 — Member

### MemberStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined label |
| IsRosterable | boolean | No | Whether a member with this status may be rostered. |
| Description | string | Yes | Administrator-facing. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### Member

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberID | integer | No | Primary key. System identifier. Never changes. |
| ReceiptNumber | string | Yes | Congregational identifier held for the first configured duration. Admin-supplied. |
| CardNumber | string | Yes | Congregational identifier issued after the configured duration. Admin-supplied. Permanent; never reassigned. |
| JoinDate | date | No |  |
| JoinReason | string | Yes | Free text. The system does not interpret, summarise, or report on it. |
| DateOfBirth | date | No |  |
| MembershipStage | string | No | Administrator-defined. Free text, not a lookup. |
| Name | string | No |  |
| Surname | string | No |  |
| Gender | string | No |  |
| Phone | string | No |  |
| Email | string | Yes |  |
| BranchID | integer | No | Foreign key → Branch |
| RoleID | integer | No | Foreign key → Role |
| MemberStatusID | integer | No | Foreign key → Member Status |

Member carries no boolean `IsActive`. A member's operational status is expressed by `MemberStatusID`.

### AttributeType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AttributeTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined attribute name |
| Description | string | Yes |  |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### MemberAttributeValue

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| MemberAttributeValueID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| AttributeTypeID | integer | No | Foreign key → Attribute Type |
| Value | string | No | Administrator-supplied |
| RecordedDate | date | No |  |
| RecordedBy | integer | No | Foreign key → Admin |

Unique on (MemberID, AttributeTypeID). Operational fact, no lifecycle flags.

### Admin

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AdminID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| PermissionTierID | integer | No | Foreign key → Permission Tier |

Records that a member has been granted admin status.

---

## D5 — Identifier History

### IdentifierHistory

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EntryID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| Type | string | No | Administrator-defined identifier category |
| Number | string | No | Administrator-supplied |
| AssignedDate | date | No |  |
| UnassignedDate | date | Yes |  |
| Reason | string | Yes |  |
| AuthorizedBy | integer | No | Foreign key → Admin |

No `IsActive` and no `IsDeleted`. Entries are historical facts.

---

## D6 — Eligibility

### Eligibility

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EligibilityID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| GrantedDate | date | No |  |
| GrantedBy | integer | No | Foreign key → Admin |
| RevokedDate | date | Yes |  |
| RevokedReason | string | Yes |  |

Each grant/revoke cycle is its own row. Absence of a row means not eligible. No `IsActive` and no `IsDeleted`.

---

## D7 — Roster Assignment

### RosterAssignment

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| DutyID | integer | No | Foreign key → Duty |
| OccurrenceID | integer | No | Foreign key → Service Occurrence |
| AssignmentStatusID | integer | Yes | Foreign key → Assignment Status |
| ApprovedBy | integer | Yes | Foreign key → Admin |
| AssignmentSource | string | No | Automatic or Manual |
| AssignedBy | integer | Yes | Foreign key → Admin. Populated only when Manual. |
| CreatedAt | timestamp | No | Basis for confirmation timeout |

Unique on (MemberID, DutyID, OccurrenceID). Operational entity. No `IsActive` and no `IsDeleted`.

---

## D8 — Attendance Record

### AttendanceRecord

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| RecordID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| OccurrenceID | integer | No | Foreign key → Service Occurrence |
| Timestamp | timestamp | No |  |
| Source | string | No | Administrator-defined. Seeded values: Manual, Tap. Tap is defined but not produced by any implemented path in the current phase. |

Unique on (MemberID, OccurrenceID). Operational entity. No `IsActive` and no `IsDeleted`.

---

## D9 — Event / Program / Program Item / Event Duty

### Event

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Primary key |
| Name | string | No |  |
| StartDate | date | No |  |
| EndDate | date | No |  |
| Type | string | No | Free text |
| HostBranchID | integer | No | Foreign key → Branch. The branch at which the event is held. |
| UsesAttendanceRegister | boolean | Yes | Null means no override at this scope. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

The Event carries no Location. The host branch expresses where the event is held.

### EventBranch

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventID | integer | No | Foreign key → Event. Part of composite primary key. |
| BranchID | integer | No | Foreign key → Branch. Part of composite primary key. |

The set of branches attending the event. Determines roster scope across branches. No lifecycle flags.

### Program

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ProgramID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event. One Program per Event. |
| Title | string | No |  |
| IsDeleted | boolean | No | False at creation. |

### ProgramItem

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ItemID | integer | No | Primary key |
| ProgramID | integer | No | Foreign key → Program |
| SequenceOrder | integer | No |  |
| Title | string | No |  |
| ScheduledStart | timestamp | No |  |
| ScheduledEnd | timestamp | No |  |
| ServiceDefID | integer | Yes | Foreign key → Service Definition |
| Location | string | Yes | Finer location than the branch |
| IsDeleted | boolean | No | False at creation. |

Program Item has no stored status field — its live state is computed from scheduled times.

### EventDuty

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| EventDutyID | integer | No | Primary key |
| EventID | integer | No | Foreign key → Event |
| DutyID | integer | No | Foreign key → Duty |
| ServiceDefID | integer | Yes | Foreign key → Service Definition. When set, the duty uses that service's per-service rules on this event. When null, the duty's global rules apply. |
| RequiredSlotCount | integer | No |  |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

An event duty references a real Duty. Eligibility for the duty comes from the Eligibility and Duty Rule modules.

---

## D10 — Config Lookups

### OutcomeState

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| OutcomeStateID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### AssignmentStatus

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AssignmentStatusID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |
| IsTerminal | boolean | No | Distinguishes slot-occupying from vacant statuses |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### PermissionTier

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PermissionTierID | integer | No | Primary key |
| Name | string | No | Administrator-defined |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### TimeOfDay

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| TimeOfDayID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### ServiceType

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ServiceTypeID | integer | No | Primary key |
| Name | string | No | Administrator-defined, open list |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### Capability

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| CapabilityID | integer | No | Primary key |
| Name | string | No | Administrator-defined action name |
| Description | string | Yes |  |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Capability names actions the system can perform. It carries no reference to Role, Admin, or Permission Tier. The assignment of capabilities is deliberately deferred.

---

## D11 — System Setting

### SystemSetting

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| Key | string | No | Primary key |
| Value | string | Yes | The configured value |
| Required | boolean | No | Absence of a required value causes the consumer to refuse to run |
| Description | string | Yes |  |

No `IsActive` and no `IsDeleted`.

---

## D12 — Materializer Run

### MaterializerRun

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

No `IsActive` and no `IsDeleted`.

---

## Configuration-layer entities

### ConfigurationAuditLog

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AuditID | integer | No | Primary key |
| EntityType | string | No | The CLR type name of the affected entity |
| EntityID | integer | No | The primary key of the affected row |
| EntityName | string | No | The affected row's name at the time of action |
| Action | string | No | Deactivate or SoftDelete |
| Reason | string | Yes | Required for SoftDelete. Optional for Deactivate. |
| InitiatedByAdminID | integer | No | Foreign key → Admin |
| ApprovedByAdminID | integer | No | Foreign key → Admin |
| InitiatedAt | timestamp | No |  |
| ApprovedAt | timestamp | No |  |
| ConsequencesPreviewed | string | No | Serialized description of the consequences shown |
| ConsequencesOccurred | string | No | Serialized description of what happened |
| NotifiedAt | timestamp | Yes | Set once the authority-notification sweep has processed this entry |

No `IsActive` and no `IsDeleted`. Audit records are permanent facts.

### EntityDeletionPolicy

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| PolicyID | integer | No | Primary key |
| EntityType | string | No | Unique. One row per configuration entity type. |
| RequiresApprovalForDelete | boolean | No |  |
| RequiresApprovalForDeactivate | boolean | No |  |
| RequiredDeletePermissionTierID | integer | Yes | Foreign key → Permission Tier |
| RequiredDeactivatePermissionTierID | integer | Yes | Foreign key → Permission Tier |
| RequiresReasonForDelete | boolean | No |  |
| RequiresReasonForDeactivate | boolean | No |  |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### NotificationSubscription

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| SubscriptionID | integer | No | Primary key |
| EventType | string | No | The kind of event that triggers the notification |
| RecipientTierID | integer | Yes | Foreign key → Permission Tier |
| RecipientAdminID | integer | Yes | Foreign key → Admin |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

Exactly one of RecipientTierID or RecipientAdminID is set per row.

---

### AttendanceRule

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AttendanceRuleID | integer | No | Primary key |
| Name | string | No | Administrator-defined label |
| TriggerType | string | No | AbsenceDays, ReadmissionCount, or Manual |
| TriggerValue | string | Yes | The trigger's value |
| OutcomeType | string | No | Notify, SetStatus, or NoOp |
| OutcomeStatusID | integer | Yes | Foreign key → Member Status. Set only when OutcomeType = SetStatus. |
| Enabled | boolean | No | When false, the rule is not evaluated. |
| IsActive | boolean | No | True at creation. |
| IsDeleted | boolean | No | False at creation. |

### AttendanceRuleScope

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| AttendanceRuleID | integer | No | Foreign key → Attendance Rule. Part of composite primary key. |
| CriteriaType | string | No | Same vocabulary as Duty Rule. |
| CriteriaValue | string | No |  |

Composite primary key on (AttendanceRuleID, CriteriaType, CriteriaValue). Multiple scope rows for one rule are ANDed.

### Readmission

| Column | Type | Nullable | Notes |
| --- | --- | --- | --- |
| ReadmissionID | integer | No | Primary key |
| MemberID | integer | No | Foreign key → Member |
| ReadmissionDate | date | No |  |
| PerformedByAdminID | integer | No | Foreign key → Admin. Records who performed the readmission. |
| Reason | string | Yes |  |

One row per readmission event. A Readmission is a historical fact. It carries no lifecycle flags and is never removed through normal operations.

## Summary of foreign keys

| From | Column | To |
| --- | --- | --- |
| DutyRule | DutyID | Duty |
| DutyRule | ServiceDefID | ServiceDefinition |
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
| ServiceOccurrence | ServiceTypeID | ServiceType |
| ServiceOccurrence | FillStatusID | OutcomeState |
| ServiceOccurrence | CreatedBy | Admin |
| ServiceOccurrence | ChangedBy | Admin |
| ServiceOccurrenceDuty | OccurrenceID | ServiceOccurrence |
| ServiceOccurrenceDuty | DutyID | Duty |
| Member | BranchID | Branch |
| Member | RoleID | Role |
| Member | MemberStatusID | MemberStatus |
| MemberAttributeValue | MemberID | Member |
| MemberAttributeValue | AttributeTypeID | AttributeType |
| MemberAttributeValue | RecordedBy | Admin |
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
| Event | HostBranchID | Branch |
| EventBranch | EventID | Event |
| EventBranch | BranchID | Branch |
| Program | EventID | Event |
| ProgramItem | ProgramID | Program |
| ProgramItem | ServiceDefID | ServiceDefinition |
| EventDuty | EventID | Event |
| EventDuty | DutyID | Duty |
| EventDuty | ServiceDefID | ServiceDefinition |
| Admin | MemberID | Member |
| Admin | PermissionTierID | PermissionTier |
| MaterializerRun | TriggeredBy | Admin |
| ConfigurationAuditLog | InitiatedByAdminID | Admin |
| ConfigurationAuditLog | ApprovedByAdminID | Admin |
| EntityDeletionPolicy | RequiredDeletePermissionTierID | PermissionTier |
| EntityDeletionPolicy | RequiredDeactivatePermissionTierID | PermissionTier |
| NotificationSubscription | RecipientTierID | PermissionTier |
| NotificationSubscription | RecipientAdminID | Admin |
| AttendanceRule | OutcomeStatusID | MemberStatus |
| AttendanceRuleScope | AttendanceRuleID | AttendanceRule |
| Readmission | MemberID | Member |
| Readmission | PerformedByAdminID | Admin |

---

*Source: System Design Specification §4, §15.*
