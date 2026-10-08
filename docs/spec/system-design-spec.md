# System Design Specification

*Architecture only — contains no configuration data from any specific congregation.*

---

## 1. Purpose

This document specifies a configurable rostering and membership platform. It describes what the system *is capable of representing and executing*. It contains no rules, names, or values belonging to any specific organization — those live in a separate configuration reference, populated by an administrator.

Nothing in this document should ever name a specific role, a specific duty, or a specific rule — not even as an illustrative example. If a sentence here could only make sense to one particular congregation, it belongs in the configuration reference instead, not here.

---

## 2. Core Design Principle

Two kinds of decisions exist, and the system keeps them strictly separate:

- **Categories** — the fixed concepts the system is built from: Member, Role, Duty, Service, Eligibility, Duty Rule, Assignment, Attendance, Confirmation, Admin, Event, Program. These exist because a rostering system needs a vocabulary to represent anything at all. They carry no organizational meaning by themselves.
- **Configuration** — every relationship, threshold, priority, permission, and outcome definition. Entered entirely by an administrator, stored as data, never hardcoded. An unconfigured category is empty and inert — a Duty with no Duty Rules attached requires nothing and permits nothing until an admin says otherwise.

No category is pre-linked to another by the system itself. Specifically: **Role and Duty have no relationship until an administrator creates one.** The system does not know, assume, or imply that any role performs any duty. That link exists only as rows in the Duty Rule table, created by an admin.

The architecture, end to end, is a chain of five distinct layers, and no layer is allowed to collapse into another:

**System vocabulary → stored facts → admin configuration → engine interpretation → operational outcomes.**

This produces four distinct categories, worth holding separately when reasoning about any part of the system:

- **Fixed** — what the system can represent, and what the engine knows how to mechanically calculate.
- **Configurable** — what an administrator chooses to express using that capability.
- **Operational data** — what has actually happened (Member records, Attendance, Roster Assignments, materialized Occurrences).
- **MKN configuration** — the particular values administrators eventually enter. Documented separately, never in this specification.

One further rule: **required configuration that a process depends on must fail loudly when absent, never idle silently.** An unconfigured category is inert and harmless; a process that depends on a specific setting to run correctly and finds that setting missing must stop and surface that fact, not proceed as if nothing were wrong.

---

## 3. System Components

| Component | Responsibility |
| --- | --- |
| Rules (configuration layer) | Stores every administrator-entered value: role/duty relationships, thresholds, tiers, permissions, outcome states |
| Eligibility | Evaluates a binary constraint for a member against a duty, based on whatever the administrator has configured |
| Priority | Orders eligible candidates according to whatever tier structure the administrator has configured for that duty |
| Assignment | Orchestrates filling a service occurrence's required duties, using Eligibility and Priority; never resolves a gap on its own initiative |
| Fill Status | Evaluates the outcome of an occurrence against administrator-defined states and triggers |
| Confirmation | Tracks each assignment's response lifecycle; re-resolves only the affected slot on a decline or timeout |
| Attendance | Records the raw fact of presence at an occurrence; no interpretation layer built in |
| Identity/Card | Manages identifier issuance and reassignment history for a member |
| Occurrence Materializer | Generates Service Occurrence records from active Service Schedules on a rolling time horizon; additive and idempotent — never modifies or deletes an existing occurrence |
| Notification Dispatcher | Sends messages through whichever channel is configured |
| Program | Manages an ordered schedule of items belonging to an Event, independent of roster/duty logic; item state is computed, not configured |

---

## 4. Core Entities

Every entity is stated with its columns, its nullability, its keys, and its relationships. Configuration entities carry both `IsActive` and `IsDeleted`. Operational entities carry neither: they are facts and are never removed through normal admin operations. `IsDeleted` implies `IsActive = false`; the two-flag lifecycle is stated in §9. A small number of entities carry neither flag and are also not configuration — for example, `AttendanceRuleScope` and `EventBranch`, whose lifecycle is derived from their parent.

### Role

| Column | Notes |
| --- | --- |
| RoleID | Primary key |
| Name | Administrator-defined label. Not unique. |
| IsDefault | Boolean. Exactly one Role is the default. The default Role cannot be soft-deleted. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Duty

| Column | Notes |
| --- | --- |
| DutyID | Primary key |
| Name | Administrator-defined label. Not unique. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

A Duty carries no reference to Role, no restriction, no priority information. A Duty is a name until Duty Rules exist for it.

### Duty Rule

| Column | Notes |
| --- | --- |
| RuleID | Primary key |
| DutyID | Foreign key → Duty |
| ServiceDefID | Foreign key → Service Definition. Optional. Null means the rule applies to the Duty wherever it appears. Set means the rule applies only where the Duty is required by that Service Definition. |
| TierOrder | Administrator-defined sequence position |
| CriteriaType | One of the supported criteria types |
| CriteriaValue | Free text. Interpreted per CriteriaType at evaluation time. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

Per-service rules override duty-global rules entirely for that (Service, Duty) pair. Same-tier rules are ANDed.

### Branch

| Column | Notes |
| --- | --- |
| BranchID | Primary key |
| Name | Administrator-defined label |
| Location | Single entry, structured internally |
| UsesAttendanceRegister | Boolean, nullable. Null means no override at this scope; the effective register state follows the global setting. Set means the branch overrides the global setting. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Branch Time Slot

| Column | Notes |
| --- | --- |
| TimeSlotID | Primary key |
| BranchID | Foreign key → Branch |
| DayOfWeek |  |
| TimeOfDayID | Foreign key → TimeOfDay |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Service Definition

| Column | Notes |
| --- | --- |
| ServiceDefID | Primary key |
| Name | Administrator-assigned label |
| ServiceTypeID | Foreign key → Service Type |
| OwningBranchID | Optional. Null means available to any branch; set means exclusive to one. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Service Definition Duty

| Column | Notes |
| --- | --- |
| ServiceDefID | Foreign key → Service Definition. Part of composite primary key. |
| DutyID | Foreign key → Duty. Part of composite primary key. |
| RequiredSlotCount | Administrator-set integer. No default. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Service Schedule

| Column | Notes |
| --- | --- |
| ScheduleID | Primary key |
| ServiceDefID | Foreign key → Service Definition |
| TimeSlotID | Foreign key → Branch Time Slot |
| StartTime | Concrete clock time |
| IsActive | Boolean. True at creation. An administrator can deactivate a schedule to stop future occurrence generation without deleting it or altering any occurrence already materialized from it. |
| IsDeleted | Boolean. False at creation. |

Active-and-not-deleted uniqueness on (ServiceDefID, TimeSlotID).

### Service Occurrence

| Column | Notes |
| --- | --- |
| OccurrenceID | Primary key |
| ScheduleID | Optional. Present for schedule-sourced occurrences. |
| EventID | Optional. Present for event-sourced occurrences. |
| Date | Date-only; start time is represented separately. |
| ServiceTypeID (override) | Optional |
| StartTime (override) | Optional |
| FillStatusID | Foreign key → Outcome State. Null at creation. |
| GeneratedBy | System or Administrator. Permanent provenance. |
| CreatedBy | Foreign key → Admin. Populated only when GeneratedBy is Administrator. |
| ChangedBy | Foreign key → Admin. Records subsequent human modification. |

A schedule-sourced row is unique on (ScheduleID, Date). This constraint does not apply to event-sourced rows. `ScheduleID` and `EventID` are mutually exclusive and exactly one must be non-null. `ScheduleID != null` and `EventID == null` is a schedule-sourced occurrence. `ScheduleID == null` and `EventID != null` is an event-sourced occurrence. The combination `null, null` and the combination `non-null, non-null` are both forbidden. The implementation must enforce this invariant. `GeneratedBy` is provenance metadata, set at creation; it is not the mechanism that makes the source invariant true. `GeneratedBy` and `CreatedBy` are bound by a check constraint: System implies CreatedBy IS NULL; Administrator implies CreatedBy IS NOT NULL. No `IsActive` and no `IsDeleted` — occurrences are operational records.

### Service Occurrence Duty

| Column | Notes |
| --- | --- |
| OccurrenceID | Foreign key → Service Occurrence. Part of composite primary key. |
| DutyID | Foreign key → Duty. Part of composite primary key. |
| Action | Added or Removed, relative to the Service Definition's normal duty list. |
| RequiredSlotCount (override) | Optional. Overrides the Definition's slot count for this date only. |

Only occurrences with an actual override need a row here. No `IsActive` and no `IsDeleted` — overrides are operational records.

### Member Status

| Column | Notes |
| --- | --- |
| MemberStatusID | Primary key |
| Name | Administrator-defined label. Seeded defaults for MKN: Active, Inactive, RA, preRA, Deceased. |
| IsRosterable | Boolean. Whether a member with this status may be rostered. Configurable. |
| Description | Optional, administrator-facing. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

The system does not interpret any status name. The `IsRosterable` flag is the mechanical property the roster engine consults. The default Member Status cannot be soft-deleted.

### Member

| Column | Notes |
| --- | --- |
| MemberID | Primary key. System identifier. Never changes. |
| ReceiptNumber | Congregational identifier held for the first configured duration. Administrator-supplied. Nullable; historical once a card is issued. |
| CardNumber | Congregational identifier issued after the configured duration. Administrator-supplied. Nullable until issued. Permanent; never reassigned. |
| JoinDate |  |
| JoinReason | Free text. The system does not interpret, summarise, or report on it. Optional. |
| DateOfBirth |  |
| MembershipStage | Administrator-defined. Free text, not a lookup. |
| Name |  |
| Surname |  |
| Gender |  |
| Phone |  |
| Email | Optional |
| BranchID | Foreign key → Branch |
| RoleID | Foreign key → Role |
| MemberStatusID | Foreign key → Member Status |

Member carries no `IsActive` and no `IsDeleted` as boolean columns. A soft-deleted member is marked via `IsDeleted` on this table; a member's operational status is expressed by `MemberStatusID`.

### Identifier History

| Column | Notes |
| --- | --- |
| EntryID | Primary key |
| MemberID | Foreign key → Member |
| Type | Administrator-defined identifier category |
| Number | Administrator-supplied |
| AssignedDate |  |
| UnassignedDate | Optional |
| Reason | Optional |
| AuthorizedBy | Foreign key → Admin |

No `IsActive` and no `IsDeleted`. Entries are historical facts and are never removed through normal operations.

### Eligibility

| Column | Notes |
| --- | --- |
| EligibilityID | Primary key |
| MemberID | Foreign key → Member |
| DutyID | Foreign key → Duty |
| GrantedDate |  |
| GrantedBy | Foreign key → Admin |
| RevokedDate | Optional |
| RevokedReason | Optional |

A binary, administrator-granted permission record for a member against a duty. Each grant/revoke cycle is its own row. No `IsActive` and no `IsDeleted`.

### Roster Assignment

| Column | Notes |
| --- | --- |
| AssignmentID | Primary key |
| MemberID | Foreign key → Member |
| DutyID | Foreign key → Duty |
| OccurrenceID | Foreign key → Service Occurrence |
| AssignmentStatusID | Foreign key → Assignment Status. Nullable — null at automatic creation. |
| ApprovedBy | Foreign key → Admin |
| AssignmentSource | Automatic or Manual |
| AssignedBy | Foreign key → Admin. Populated only when Manual. |
| CreatedAt | Timestamp. Basis for confirmation timeout. |

Unique on (MemberID, DutyID, OccurrenceID). No `IsActive` and no `IsDeleted` — assignments are operational records.

### Outcome State (lookup)

| Column | Notes |
| --- | --- |
| OutcomeStateID | Primary key |
| Name | Administrator-defined, open list |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Assignment Status (lookup)

| Column | Notes |
| --- | --- |
| AssignmentStatusID | Primary key |
| Name | Administrator-defined, open list |
| IsTerminal | Boolean. Distinguishes statuses that occupy a required slot from statuses that leave the slot vacant. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Permission Tier (lookup)

| Column | Notes |
| --- | --- |
| PermissionTierID | Primary key |
| Name | Administrator-defined. The system names no tier. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Time Of Day (lookup)

| Column | Notes |
| --- | --- |
| TimeOfDayID | Primary key |
| Name | Administrator-defined, open list |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Service Type (lookup)

| Column | Notes |
| --- | --- |
| ServiceTypeID | Primary key |
| Name | Administrator-defined, open list |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Capability (lookup)

| Column | Notes |
| --- | --- |
| CapabilityID | Primary key |
| Name | Administrator-defined action name |
| Description | Optional |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

The Capability vocabulary names actions the system can perform. No relationship to Role, Admin, or Permission Tier. The assignment of capabilities to those entities is deliberately deferred.

### Attribute Type

| Column | Notes |
| --- | --- |
| AttributeTypeID | Primary key |
| Name | Administrator-defined attribute name |
| Description | Optional |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

The system does not interpret any attribute name. Attributes are configured by the admin.

### Member Attribute Value

| Column | Notes |
| --- | --- |
| MemberAttributeValueID | Primary key |
| MemberID | Foreign key → Member |
| AttributeTypeID | Foreign key → Attribute Type |
| Value | Administrator-supplied string |
| RecordedDate |  |
| RecordedBy | Foreign key → Admin |

Unique on (MemberID, AttributeTypeID). Absence of a row means the attribute is not set. No `IsActive` and no `IsDeleted`.

### Admin

| Column | Notes |
| --- | --- |
| AdminID | Primary key |
| MemberID | Foreign key → Member |
| PermissionTierID | Foreign key → Permission Tier |

An Admin row records that a member has been granted admin status by another admin. No member is inherently an admin.

### Attendance Record

| Column | Notes |
| --- | --- |
| RecordID | Primary key |
| MemberID | Foreign key → Member |
| OccurrenceID | Foreign key → Service Occurrence |
| Timestamp |  |
| Source | Administrator-defined. Seeded values: Manual, Tap. Manual is produced by the manual register marking path, which is implemented. Tap is defined in the vocabulary but is not produced by any implemented path in the current phase. |

Unique on (MemberID, OccurrenceID). Operational fact. No `IsActive` and no `IsDeleted`.

### Event

| Column | Notes |
| --- | --- |
| EventID | Primary key |
| Name |  |
| StartDate |  |
| EndDate |  |
| Type | Free text |
| HostBranchID | Foreign key → Branch. The branch at which the event is held. |
| UsesAttendanceRegister | Boolean, nullable. Null means no override at this scope; the effective register state follows the next scope in the resolution order. Set means the event overrides the other scopes for its own occurrences. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

The Event carries no Location. The host branch expresses where the event is held. A finer physical location, where required, is carried on the Program Item.

### Event Branch

| Column | Notes |
| --- | --- |
| EventID | Foreign key → Event. Part of composite primary key. |
| BranchID | Foreign key → Branch. Part of composite primary key. |

The set of branches attending the event. Determines roster scope across branches. No `IsActive` and no `IsDeleted` — the set is derived from the Event's own state.

### Program

| Column | Notes |
| --- | --- |
| ProgramID | Primary key |
| EventID | Foreign key → Event. One Program per Event. |
| Title |  |
| IsDeleted | Boolean. False at creation. |

### Program Item

| Column | Notes |
| --- | --- |
| ItemID | Primary key |
| ProgramID | Foreign key → Program |
| SequenceOrder |  |
| Title |  |
| ScheduledStart |  |
| ScheduledEnd |  |
| ServiceDefID | Foreign key → Service Definition. Optional. |
| Location | Optional. Finer location than the branch. |
| IsDeleted | Boolean. False at creation. |

Program Item has no stored status field — its live state is computed at read time from scheduled times.

### Event Duty

| Column | Notes |
| --- | --- |
| EventDutyID | Primary key |
| EventID | Foreign key → Event |
| DutyID | Foreign key → Duty |
| ServiceDefID | Foreign key → Service Definition. Optional. When set, the duty uses that service's per-service rules on this event. When null, the duty's global rules apply. |
| RequiredSlotCount |  |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

An event duty references a real Duty. Eligibility for the duty comes from the Eligibility and Duty Rule modules. No parallel eligibility path.

### System Setting

| Column | Notes |
| --- | --- |
| Key | Primary key |
| Value | Optional |
| Required | Boolean |
| Description | Optional |

A generic store for scalar, non-list configuration values. No `IsActive` and no `IsDeleted`.

### Materializer Run

| Column | Notes |
| --- | --- |
| RunID | Primary key |
| TriggerType | Scheduled or Manual |
| TriggeredBy | Foreign key → Admin. Populated only when TriggerType is Manual. |
| StartedAt |  |
| CompletedAt | Optional |
| SchedulesEvaluated | Count |
| OccurrencesCreated | Count |
| Status | Success or Failure |
| ErrorDetail | Optional |

Check constraints enforce attribution and completion consistency. No `IsActive` and no `IsDeleted`.

### Configuration Audit Log

| Column | Notes |
| --- | --- |
| AuditID | Primary key |
| EntityType | The CLR type name of the affected entity |
| EntityID | The primary key of the affected row |
| EntityName | The affected row's name at the time of action |
| Action | Deactivate or SoftDelete |
| Reason | Required for SoftDelete. Optional for Deactivate. |
| InitiatedByAdminID | Foreign key → Admin |
| ApprovedByAdminID | Foreign key → Admin |
| InitiatedAt |  |
| ApprovedAt |  |
| ConsequencesPreviewed | Serialized description of the consequences shown to the admin |
| ConsequencesOccurred | Serialized description of what actually happened |
| NotifiedAt | Optional. Set once the authority-notification sweep has processed this entry. |

No `IsActive` and no `IsDeleted` — audit records are permanent facts.

### Entity Deletion Policy

| Column | Notes |
| --- | --- |
| PolicyID | Primary key |
| EntityType | Unique. One row per configuration entity type. |
| RequiresApprovalForDelete | Boolean |
| RequiresApprovalForDeactivate | Boolean |
| RequiredDeletePermissionTierID | Foreign key → Permission Tier. Optional. |
| RequiredDeactivatePermissionTierID | Foreign key → Permission Tier. Optional. |
| RequiresReasonForDelete | Boolean |
| RequiresReasonForDeactivate | Boolean |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

### Notification Subscription

| Column | Notes |
| --- | --- |
| SubscriptionID | Primary key |
| EventType | The kind of event that triggers the notification |
| RecipientTierID | Foreign key → Permission Tier. Optional. |
| RecipientAdminID | Foreign key → Admin. Optional. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

Exactly one of RecipientTierID or RecipientAdminID is set per row. When the tier is set, all admins of that tier receive the notification. When the specific admin is set, only that admin receives it.

### Attendance Rule

| Column | Notes |
| --- | --- |
| AttendanceRuleID | Primary key |
| Name | Administrator-defined label |
| TriggerType | One of: AbsenceDays, ReadmissionCount, Manual |
| TriggerValue | The trigger's value. For AbsenceDays, the number of days. For ReadmissionCount, the count. For Manual, unused. |
| OutcomeType | One of: Notify, SetStatus, NoOp |
| OutcomeStatusID | Foreign key → Member Status. Set only when OutcomeType = SetStatus. |
| Enabled | Boolean. When false, the rule is not evaluated. |
| IsActive | Boolean. True at creation. |
| IsDeleted | Boolean. False at creation. |

An Attendance Rule is a rule the admin creates to apply attendance-driven outcomes. The system evaluates the rule against the member population on a schedule. The rule's scope is expressed through the Attendance Rule Scope join table. A rule with no scope rows applies to every member.

### Attendance Rule Scope

| Column | Notes |
| --- | --- |
| AttendanceRuleID | Foreign key → Attendance Rule. Part of composite primary key. |
| CriteriaType | One of the supported criteria types. Same vocabulary as Duty Rule. |
| CriteriaValue | The value for that criteria. |

Composite primary key on (AttendanceRuleID, CriteriaType, CriteriaValue). Multiple scope rows for one rule are ANDed. A rule with no scope rows applies to every member.

### Readmission

| Column | Notes |
| --- | --- |
| ReadmissionID | Primary key |
| MemberID | Foreign key → Member |
| ReadmissionDate | The date the member was readmitted. |
| PerformedByAdminID | Foreign key → Admin. Records who performed the readmission. |
| Reason | Optional. |

One row per readmission event. A Readmission is a historical fact. It carries no lifecycle flags and is never removed through normal operations. A member's readmission count is the number of Readmission rows for that member.


### Temporal types

The schema uses three temporal types. Each has one meaning.

**`timestamp`** represents an instant. It is persisted in UTC. Every schema property classified as `timestamp` carries this meaning, regardless of the column's name or purpose. Current members of the classification include `RosterAssignment.CreatedAt`, `AttendanceRecord.Timestamp`, `MaterializerRun.StartedAt` and `CompletedAt`, `ConfigurationAuditLog.InitiatedAt`, `ApprovedAt`, and `NotifiedAt`, `ProgramItem.ScheduledStart` and `ScheduledEnd`, and any future column added to the schema as an instant.

**`date`** represents a calendar date with no time and no timezone. It is not converted through UTC, and it is not interpreted as a midnight instant. Current members include `ServiceOccurrence.Date`, `Member.JoinDate`, `DateOfBirth`, `IdentifierHistory.AssignedDate` and `UnassignedDate`, `Eligibility.GrantedDate` and `RevokedDate`, `Readmission.ReadmissionDate`, `MemberAttributeValue.RecordedDate`, and `Event.StartDate` and `EndDate`.

**`time`** represents a wall-clock time with no date and no timezone. Current members include `ServiceOccurrence.StartTime` and `ServiceSchedule.StartTime`.

**`ApplicationTimeZone`** is used for exactly one purpose: converting an instant to the calendar date it falls on in that zone. The canonical conversion is `TimeZoneResolver.ToDateInTimeZone(instant, zone)`, which yields a `DateOnly`. Every process that derives "today" from the current instant uses this conversion. The materializer, 5.0, 7.0, 12.0, and 13.0 all derive their operational date through it.

`date` and `time` values are never converted through UTC. A date is the date the administrator entered; a time is the wall-clock time the administrator entered. The only place a timezone participates is in the conversion of an instant to a date.

---

## 5. The Configuration Language

**Supported criteria types** (the fixed, preset vocabulary a Duty Rule's CriteriaType may reference):

- Role
- Gender
- Age Range
- Tenure (duration since joining)
- Membership Stage
- Branch-Attendance Recency
- Eligibility Flag (references the Eligibility table)
- Member Attribute (references an admin-defined Attribute Type and a required value)
- Youth (computed from DateOfBirth against the configured YouthAgeMin and YouthAgeMax settings; carries no value)
- (Reserved for future use once historical data exists) Acceptance Rate, Duties Carried, Days Since Last Assignment

These are supported criterion *types*, not rules. Their presence in this vocabulary does not activate them anywhere.

The vocabulary is fixed, and each criterion's value language is fixed by §10.1.5. The administrator chooses the value; the system does not accept a value that does not match the criterion's grammar.

**Tier composition:** a tier may combine more than one criterion. Multiple Duty Rule rows may share the same TierOrder for a given Duty, and all rows sharing a TierOrder must be satisfied for that tier to match — an AND across whatever criteria the administrator has placed at that tier. No OR operator is provided within a tier, because tiers themselves already express ordered-preference OR. No NOT operator is provided.

**Tier evaluation — two separate mechanisms:** Duty Rule governs ranking, not candidacy. Whether a member is a candidate at all is governed separately by Eligibility.

1. No Duty Rules configured for a duty → no rule-based ordering or filtering is applied → every eligible member enters as an equal candidate.
2. Duty Rules exist for a duty → the engine evaluates configured tiers in TierOrder → the first tier producing at least one eligible, available candidate supplies the candidate set.

---

## 6. What Remains Fixed, and Why

The entity categories in §4 and the criteria-type vocabulary in §5 are the fixed system vocabulary. Other fixed behavior exists only where the system must perform a defined mechanical operation on stored facts — deriving a Program Item's lifecycle state from scheduled times, or materializing Service Occurrences from active Service Schedules on a rolling horizon (§7). Neither is a category, and neither is an administrator setting; both are computed or generated as a matter of correct operation. No organization-specific operating rule is ever fixed by the system.

---

## 7. Resolved Design Decision — Occurrence Materialization

**Question:** does generating an actual Service Occurrence from a recurring Service Schedule happen as an administrator action, or as a system-internal process?

**Decision: system-internal, time-triggered, on a rolling horizon.**

### Trigger and scheduling

Runs as part of the application's existing scheduled-job mechanism. Runs daily, a fixed frequency, not administrator-configurable. Every run evaluates the complete current horizon, so a missed day requires no checkpoint or recovery state. Overlapping runs are not allowed. A manual trigger is available and uses exactly the same logic as the scheduled run, differing only in trigger source; it accepts an explicit horizon in days, and a smaller manual horizon is a safe no-op for dates already covered.

### Horizon configuration

`OccurrenceHorizonDays` is a required System Setting — integer, unit days, minimum 0, global rather than per-branch or per-schedule. If unconfigured, the Materializer refuses to run and surfaces a configuration error. A change to the horizon takes effect on the next run; changing the setting is not itself a trigger.

### Schedule lifecycle interaction

A newly created schedule is picked up automatically on the next run. When a schedule changes, occurrences already materialized from it are never rewritten. When a schedule is deactivated, existing occurrences remain exactly as they are, and the schedule stops being eligible for future generation. Changes to a Service Definition's duty composition are never propagated backward into already-materialized occurrences.

### Occurrence creation contract

For each active schedule, the process generates candidate dates from today through today + horizon, inclusive, using the configured system/application timezone. For each date, it checks whether a Service Occurrence already exists for that (ScheduleID, Date) pair; if one does, nothing happens; if not, it creates one, inheriting the schedule's service type and start time, with FillStatusID null, ChangedBy null, GeneratedBy set to System, and CreatedBy null. It does not create Service Occurrence Duty rows. An occurrence is created even if its Service Definition currently has zero configured duties. Each occurrence commits in its own transaction. The process is fully re-entrant with no checkpoint state.

A schedule is eligible for materialization only when the schedule, its Service Definition, and its Branch Time Slot are all active.

### Immutability

An existing occurrence is never updated or deleted by this process, without exception. Cancellation is represented through Outcome State, not row deletion. GeneratedBy is permanent provenance, set once and never altered; ChangedBy separately records any later human modification.

### Time handling

Date is date-only; start time is represented separately and is unaffected by this process. No daylight-saving-specific logic exists here. No holiday or blackout exclusions are built in.

### Operational visibility

Each run produces a Materializer Run record — trigger type, counts, success or failure. A failed run is recoverable simply by running again. No dry-run mode and no un-materialize operation exist.

### Explicit non-relationships

This process is independent of Assignment generation. It never sends a notification. Roster generation queries occurrences directly; no readiness flag is required. Retention/archival policy and schedule-overlap or conflict detection are out of scope. Enforcing which branch may use a given Service Definition belongs to schedule/configuration validation, not to this process.

### Event-sourced occurrences

Occurrences created when an administrator links a Program Item directly to a Service Definition are entirely outside this process. Their values are derived from the Program Item, not from a Service Schedule:

- `Date` — the date portion of `ProgramItem.ScheduledStart`
- `StartTime` — the time portion of `ProgramItem.ScheduledStart`
- `ServiceTypeID` — inherited from the linked `ServiceDefinition.ServiceTypeID`
- `EventID` — from the Program Item's Event
- `ScheduleID` — null

They carry `GeneratedBy = Administrator` and `CreatedBy` = the admin who created them, and their uniqueness is governed by the Event/Program process, not by the (ScheduleID, Date) rule.

This is the opposite of the schedule-sourced case, where `Date`, `StartTime`, and `ServiceTypeID` all derive from `ServiceSchedule` and its associated `ServiceDefinition`.

### The resulting invariant

The Occurrence Materializer is additive and idempotent. It creates missing schedule-sourced occurrences within the configured horizon and never modifies or deletes an occurrence that already exists.

---

## 8. Occurrence Materialization — Derived Consequences

*This appendix derives, from the locked contract in §7, the concrete schema behavior, data flows, invariants, and indexing the Occurrence Materializer requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §8.5 rather than assumed.*

### 8.0 Process Boundary

11.0 Materialize Occurrences is the process that ensures Service Occurrence rows exist for every active Service Schedule up to a configured horizon, expressed in days. Its primary trigger is time; its secondary trigger is an optional manual invocation by an administrator.

It reads configuration and one setting, writes operational records, and invokes no other process. It is placed in Operations rather than Configuration because it produces operational records from configuration data.

**At a glance:**

- **Trigger:** scheduled run (daily); manual administrator action with an explicit horizon.
- **Reads:** D3 ServiceSchedule (active), D3 BranchTimeSlot, D3 ServiceDefinition, D3 ServiceOccurrence (existence check), D11 SystemSetting (`OccurrenceHorizonDays`).
- **Writes:** D3 ServiceOccurrence (inserts only), D12 MaterializerRun.
- **Does not:** modify or delete an existing occurrence, invoke any other process, read any other store.

### 8.1 Derived Schema Behavior

#### 8.1.1 ServiceOccurrence — column semantics under materialization

The ServiceOccurrence table is defined in §4. This section states, for each column, what the Materializer does with it.

| Column | Materializer behavior |
| --- | --- |
| OccurrenceID | System-generated. |
| ScheduleID | Populated with the schedule's ID. Never null for a row this process creates. |
| EventID | Always null for rows this process creates. |
| Date | The missing date being materialized. Date-only. |
| ServiceTypeID (override) | Inherited as a value from the schedule's Service Definition at creation time. |
| StartTime (override) | Inherited as a value from the schedule at creation time. |
| FillStatusID | Left null. Populated only by 10.0 Evaluate Fill Status. |
| GeneratedBy | Set to System. Permanent. |
| CreatedBy | Left null. Populated only when GeneratedBy = Administrator. |
| ChangedBy | Left null. Never set at creation. |

#### 8.1.2 Provenance semantics — the four reachable states

| State | GeneratedBy | CreatedBy | ChangedBy | Meaning |
| --- | --- | --- | --- | --- |
| Just materialized | System | NULL | NULL | System created it; nothing has touched it since. |
| Materialized, then admin-edited | System | NULL | Admin | System created it; an admin subsequently changed it. |
| Admin-created (event-sourced) | Administrator | Admin | NULL | An admin created it; nothing has touched it since. |
| Admin-created, then admin-edited | Administrator | Admin | Admin | An admin created it; an admin subsequently changed it. |

Two check constraints enforce the only nontrivial relationship among the three columns:

- `GeneratedBy = 'System'` implies `CreatedBy IS NULL`.
- `GeneratedBy = 'Administrator'` implies `CreatedBy IS NOT NULL`.

Together these establish the equivalence `CreatedBy IS NOT NULL ⇔ GeneratedBy = 'Administrator'`.

#### 8.1.3 Uniqueness

A schedule-sourced occurrence is unique on (ScheduleID, Date). Event-sourced occurrences are excluded — they have ScheduleID = NULL, and their uniqueness is governed by the Event/Program process.

The uniqueness constraint is the database-level correctness guard against duplicate creation. The application-level existence check is a pre-write optimization, not the guarantee.

#### 8.1.4 Schedule eligibility

A schedule is eligible for materialization when the schedule itself, its Service Definition, and its Branch Time Slot are all active.

#### 8.1.5 Materializer Run — the operational record

Each run of the Materializer produces one Materializer Run record. Fields: RunID, TriggerType, TriggeredBy, StartedAt, CompletedAt, SchedulesEvaluated, OccurrencesCreated, Status, ErrorDetail.

Two consistency rules apply: a Manual trigger requires a non-null TriggeredBy; a Scheduled trigger requires a null one. A Materializer Run record is written for both successful and failed runs.

#### 8.1.6 System Setting — required-value semantics

`OccurrenceHorizonDays` is a required System Setting. If unset, the Materializer refuses to run and surfaces a configuration error. It does not idle silently and does not use an assumed default.

### 8.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | SystemSetting where Key = 'OccurrenceHorizonDays' | Determine N. |
| R2 | System/application timezone configuration | Resolve "today" deterministically. |
| R3 | ServiceSchedule where IsActive = true, joined to BranchTimeSlot and ServiceDefinition | Enumerate schedules needing coverage. |
| R4 | ServiceOccurrence where ScheduleID = ? and Date in target range | Compute the missing-date set per schedule. |

R3 reads ServiceDefinition.ServiceTypeID as a value to inherit. It does not read the ServiceType lookup table itself. R3 does not read ServiceDefinition.OwningBranchID — OwningBranch enforcement is a configuration-validation concern, not this process's concern.

**Writes:**

| # | Target | Fields | Transaction boundary |
| --- | --- | --- | --- |
| W1 | ServiceOccurrence | ScheduleID, Date, ServiceTypeID, StartTime, FillStatusID = NULL, GeneratedBy = 'System', CreatedBy = NULL, ChangedBy = NULL | One transaction per row. |
| W2 | MaterializerRun | Full record, written at completion whether successful or failed. | One record per run. |

**Stores never touched:** Role, Duty, DutyRule, Member, IdentifierHistory, Eligibility, RosterAssignment, AttendanceRecord, ServiceOccurrenceDuty, Event, Program, ProgramItem, EventDuty, OutcomeState, AssignmentStatus, PermissionTier, ServiceDefinitionDuty, ServiceType, TimeOfDay, Branch, and Admin (except via TriggeredBy on a manual run).

### 8.3 Invariants

**Additivity.** The Materializer issues no UPDATE and no DELETE against ServiceOccurrence, ever.

**Idempotency.** Running the Materializer twice at the same system time with the same horizon produces exactly the same set of ServiceOccurrence rows as running it once.

**Horizon boundedness.** No row created by this process has a Date outside [today, today + N], inclusive.

**Schedule-source attribution.** Every row created by this process has a non-null ScheduleID, GeneratedBy = 'System', CreatedBy = NULL, ChangedBy = NULL, and FillStatusID = NULL.

**Provenance immutability.** Once a row exists, its GeneratedBy value is never altered.

**System-actor consistency.** A row with GeneratedBy = 'System' has a null CreatedBy; a row with GeneratedBy = 'Administrator' has a non-null CreatedBy.

**Schedule deactivation semantics.** Deactivating a ServiceSchedule stops new occurrence creation for that schedule and does not affect any existing occurrence.

**Schedule change semantics.** Changing a ServiceSchedule does not rewrite any occurrence already materialized from it.

**Service Definition change semantics.** Changing a ServiceDefinition's duty composition does not propagate to any existing occurrence.

**Full re-entrancy.** A partially completed run leaves the system in a state that the next run will complete correctly, with no checkpoint state.

**Manual and scheduled equivalence.** For the same horizon and system time, a manual run and a scheduled run produce identical results.

**Required-setting failure.** If OccurrenceHorizonDays is unset, zero occurrences are written and one Materializer Run is recorded with Status = Failure.

**Uniqueness.** No two schedule-sourced rows share the same (ScheduleID, Date) pair.

**Event-sourced exclusion.** The Materializer never creates, modifies, or deletes a row with a non-null EventID.

**Zero-duty occurrence creation.** A schedule whose Service Definition has zero configured duties still produces occurrence rows.

**Timezone determinism.** For a given system timezone and instant, "today" resolves to a single calendar date across all runs.

**No outbound flow.** No notification, no assignment generation, no output to any process or store other than ServiceOccurrence and MaterializerRun.

**Non-propagation of overrides.** An existing ServiceOccurrenceDuty row is never modified by this process, and its presence on an occurrence does not cause the Materializer to skip or recreate that occurrence.

### 8.4 Indexes

**Unique index on ServiceOccurrence (ScheduleID, Date), restricted to rows where ScheduleID IS NOT NULL.**

Serves two purposes: the correctness guard against duplicate creation, and the backing index for the existence check (R4). No separate index is created for the existence check.

Physical expression of "restricted to schedule-sourced rows" is an implementation choice (see §8.5).

**Partial index on ServiceSchedule (IsActive) where IsActive = true.**

Serves R3. The table is small, so the benefit is modest, but the partial index matches the query exactly.

### 8.5 Explicitly Open Implementation Choices

- **Physical column types.** The semantics of GeneratedBy, TriggerType, and Status are fixed. Whether each is a database enum, a short string with a check constraint, or a lookup reference is an implementation choice.
- **Unique-constraint expression across database engines.** The rule is (ScheduleID, Date) unique, restricted to non-null ScheduleID. The mechanism — filtered unique index, partial unique index, or NULL-distinct composite uniqueness — depends on the target database.
- **Non-required setting with no value.** The behavior when a Required = false setting has no value is defined per-setting. No such setting currently exists in this contract.
- **Retention and archival.** Out of scope.

*End of §8. This section derives from §7 and does not extend it.*

---

## 9. Configuration Layer — Derived Consequences

*This appendix derives, from the locked contracts for 1.0 through 4.0 and 8.0, the concrete schema behavior, data flows, invariants, and indexing the configuration layer requires. Every consequence traces to a locked contract. The two items that were decisions — schedule uniqueness (D1) and the OutcomeState mapping (D2) — were explicitly locked before this appendix was written. Where an implementation choice remains genuinely open, it is named in §9.5.*

### 9.0 Configuration Layer Boundary

The configuration layer is the set of processes through which an administrator enters every organizational choice the system is capable of expressing. Its outputs are inert data — rows in D1, D2, D3, D4, D5, D6, D9, and D10 — that operational processes read.

It has one direction of dependency: configuration → operations. Configuration processes do not read operational stores and do not invoke operational processes. They write only to configuration stores, with a single explicit exception: **8.0 Manage Events and Programs is the sole configuration process permitted to create an operational record**, and only because a Program Item has been explicitly linked to a Service Definition. That write produces an event-sourced ServiceOccurrence row and is the only operational write the configuration layer performs.

**At a glance:**

- **Processes:** 1.0 Configure vocabulary, 2.0 Configure duty rules, 3.0 Manage membership, 4.0 Manage eligibility, 8.0 Manage events and programs.
- **Reads:** configuration stores only.
- **Writes:** configuration stores only, plus the single 8.0 exception above.
- **Does not touch:** D7 Roster Assignment, D8 Attendance Record, D12 Materializer Run, and any operational process.

### 9.1 Derived Schema Behavior

#### 9.1.1 Configuration entity lifecycle

Configuration entities whose contracts define lifecycle state use immediate activation and deactivation rather than deletion. Lifecycle semantics are defined per entity in the sections that follow. Entities whose contracts do not define lifecycle state — IdentifierHistory, Eligibility (which uses grant/revoke), SystemSetting, and D10 lookup values — are governed by their own specific rules and do not carry an active/inactive flag unless their contract says so.

For entities that do carry lifecycle state, the rules are uniform:

- **Creation is immediate.** An entity is active from the moment it is created.
- **Deactivation removes the entity from future use** without altering any record that references it.
- **Every field is editable, always.** Edits take effect for future use.
- **No deletion, ever.** Configuration entities are historical facts.

Entities with lifecycle state: Role, Duty, Branch, BranchTimeSlot, ServiceDefinition, ServiceDefinitionDuty, ServiceSchedule, Member, DutyRule, Event, Program, ProgramItem, EventDuty.

Entities without lifecycle state: IdentifierHistory, Eligibility, SystemSetting, Admin, D10 lookup values.

#### 9.1.2 Role and Duty

Role and Duty are pure names plus an active flag. Neither carries any reference to the other. The relationship — when one exists — is created in 2.0 as a DutyRule row, and lives on the Duty side.

- Names are required but not unique.
- Role and Duty cannot be linked at creation time by any means other than a subsequent DutyRule row.
- An unreferenced Role or Duty is valid, inert, and harmless.

#### 9.1.3 Branch, Branch Time Slot, Time of Day

Branch carries a required structured Location value. The internal structure of Location is an implementation choice (§9.5).

BranchTimeSlot is the join of Branch × DayOfWeek × TimeOfDay. Multiple slots per (BranchID, DayOfWeek) are permitted. TimeOfDayID references an admin-defined lookup in D10; it is a descriptive classification, not a time.

TimeOfDay is a lookup — an open, administrator-defined list.

#### 9.1.4 Service Definition and Service Definition Duty

ServiceDefinition carries a required ServiceTypeID referencing the ServiceType lookup in D10, an optional OwningBranchID, and a Name. It is not required to have any duties.

ServiceDefinitionDuty is the join of ServiceDefinition × Duty, carrying RequiredSlotCount. It inherits the deactivation model.

#### 9.1.5 Service Schedule

ServiceSchedule is the join of ServiceDefinition × BranchTimeSlot, carrying StartTime and IsActive.

- StartTime lives here, not on ServiceDefinition and not on BranchTimeSlot, because it varies per branch, per slot, and per service simultaneously.
- IsActive = true at creation.
- **An active ServiceSchedule is uniquely identified by the pair (ServiceDefID, TimeSlotID). The uniqueness constraint applies only to active rows.**

The uniqueness rule is a locked decision (D1). Inactive schedules are excluded, so a deactivated schedule may be replaced. StartTime does not participate in identity. The database constraint is the correctness guard; duplicate creation fails explicitly rather than silently deduplicating.

#### 9.1.6 OwningBranch enforcement

The OwningBranchID on a ServiceDefinition is enforced as a hard data-integrity invariant.

- If OwningBranchID is null, any BranchTimeSlot may be used.
- If OwningBranchID is set, the BranchTimeSlot referenced by a ServiceSchedule for that ServiceDefinition must have a BranchID equal to OwningBranchID.
- A ServiceSchedule whose TimeSlot.BranchID differs from its ServiceDefinition.OwningBranchID (when the latter is non-null) is an integrity violation.

The invariant crosses tables. The implementation must enforce it. The mechanism is implementation choice. Validation at schedule creation is not sufficient on its own, because a later change to OwningBranchID or to a referenced BranchTimeSlot would otherwise invalidate the invariant silently.

#### 9.1.7 Member and Identifier History

Member carries its register fields plus a single BranchID and a single RoleID. Required at creation: JoinDate, DateOfBirth, MembershipStage, Name, Surname, Gender, Phone, BranchID, RoleID. Only Email is optional.

MembershipStage is a free-text value, not a lookup.

IdentifierHistory is a per-member log of identifier assignments. Type is free-text. Number is admin-supplied. AssignedDate and Number are required; UnassignedDate and Reason are optional; AuthorizedBy is a required FK to Admin. No active flag. To retire an identifier, UnassignedDate and optionally Reason are set. Entries are never deleted.

Readmission is a per-member historical fact recorded when a member is readmitted. Process 3.0 owns its creation. The administrator performing the readmission is recorded in `PerformedByAdminID`; the date is recorded in `ReadmissionDate`; an optional `Reason` may be supplied. A Readmission carries no lifecycle flags and is never removed through normal operations. Creating a Readmission does not modify `Member.MemberStatusID`; the status change, if any, is a separate administrative action or a separate Attendance Rule outcome. The system does not require the member to have held any particular status or to have been absent before a readmission can be recorded. No correction or deletion mechanism for Readmission is currently defined; if one becomes necessary, it is a specification amendment.

Deactivating a Member excludes them from new roster generation. It does not touch operational records. Changing a Member's BranchID or RoleID affects only future use.

#### 9.1.8 Admin and Permission Tier

Admin links a Member to a PermissionTier. PermissionTier is an admin-defined lookup in D10, deliberately minimal. The capability matrix is not defined by the schema; enforcement is upstream.

#### 9.1.9 Eligibility

Eligibility is a grant/revoke record keyed by EligibilityID. Each grant/revoke cycle is its own row. The applicable ordering rule — most recent grant by chronology, or by EligibilityID — is determined by the process reading the record (5.0), not by the schema. Section 10 of this specification states the rule that process uses.

The absence of a row means the member is not eligible. Eligibility is opt-in.

#### 9.1.10 Duty Rule

DutyRule is a row in D2 carrying DutyID, TierOrder, CriteriaType, CriteriaValue. Multiple rows may share TierOrder for a single Duty; those rows are ANDed. TierOrder gaps are permitted. Deactivated rules are invisible to 5.2.

CriteriaType is fixed to the §5 vocabulary at write time. CriteriaValue is free-text in storage, interpreted per CriteriaType at evaluation time.

#### 9.1.11 Event, Program, Program Item, Event Duty

Event carries the fields listed in §4. Type is free-text.

Program is one per Event. ProgramItem carries SequenceOrder, Title, ScheduledStart, ScheduledEnd, and an optional ServiceDefID. It has no stored status field; live state is computed from scheduled times.

When a ProgramItem links to a ServiceDefinition, an event-sourced ServiceOccurrence is created. It has EventID populated, ScheduleID null, Date and StartTime derived from ProgramItem.ScheduledStart, ServiceTypeID inherited from the linked ServiceDefinition, GeneratedBy = Administrator, and CreatedBy = the admin who performed the action. This is the only write the configuration layer performs to an operational store.

EventDuty carries a free-text Label, a direct AssignedMemberID, and an optional AssignmentStatusID. It does not reference the Duty table.

Event and EventBranch carry the following relational rules. `HostBranchID` must reference a Branch. `EventBranch` may contain zero or more branches. The host and attendee sets are independent: the host need not appear in `EventBranch`, and a branch may appear as both host and attendee. Inactive branches may remain referenced. The composite primary key on (EventID, BranchID) prevents duplicates. No constraint requires `HostBranchID` to appear in `EventBranch`.

#### 9.1.12 D10 — Config Lookups

D10 contains admin-defined lookups referenced elsewhere in the schema: Outcome State, Assignment Status, Permission Tier, TimeOfDay, ServiceType.

#### 9.1.13 System Setting — configuration values consumed by other processes

The configuration layer writes SystemSetting rows. Some are consumed only by operational processes:

- OccurrenceHorizonDays — required; consumed by 11.0.
- InitialAssignmentStatusID, DeclinedStatusID, TimedOutStatusID — required; consumed by 7.0.
- OutcomeStateUnfilledID, OutcomeStatePartiallyFilledID, OutcomeStateFilledID — required; consumed by 10.0.
- OutcomeStateCancelledID — non-required; consumed by 10.0.
- NotificationChannel — deployment choice; consumed by 9.0.
- ReceiptToCardDurationDays, ConfirmationTimeoutHours, YouthAgeMin, YouthAgeMax, and the global attendance register scope — consumed by whichever processes reference them.

The settings AgeRangeMin, AgeRangeMax, and TenureThresholdDays were used by earlier revisions to provide fallback values for the Age Range and Tenure criteria. They are removed. The criterion value is the sole source; there is no fallback.

A required setting with no value causes the consuming process to refuse to run and surface a configuration error.

### 9.2 Read and Write Footprint

**Reads:**

| # | Process | Reads |
| --- | --- | --- |
| R1 | 1.1 | D1 (existing Roles). Reads no D10 lookup. |
| R2 | 1.2 | D1 (existing Duties) |
| R3 | 1.3 | D3 (Branch) |
| R4 | 1.4 | D3 (Branch for FK selection); D10 (TimeOfDay — validates TimeOfDayId) |
| R5 | 1.5 | D10 (ServiceType — validates ServiceTypeId); D3 (Branch for OwningBranchID selection) |
| R6 | 1.6 | D1 (Role/Duty); D3 (ServiceDefinition, BranchTimeSlot). Reads no D10 lookup directly. |
| R7 | 2.0 | D1 (Duty); D2 (existing DutyRules) |
| R8 | 3.1 | D4 (edit/display); D1 (Role); D3 (Branch) |
| R9 | 3.2 | D5 (IdentifierHistory); D4 (Member) |
| R10 | 4.0 | D1 (Duty); D4 (Member); D6 (existing Eligibility) |
| R11 | 8.0 | D9; D3 (ServiceDefinition); D4 (Member); D10 (AssignmentStatus — validates AssignmentStatusId on EventDuty) |

**Writes:**

| # | Process | Writes |
| --- | --- | --- |
| W1 | 1.0 (all subprocesses) | D1, D3, D10 |
| W2 | 2.0 | D2 |
| W3 | 3.1 | D4 |
| W4 | 3.2 | D5 |
| W5 | 4.0 | D6 |
| W6 | 8.0 | D9; D3 (ServiceOccurrence for event-sourced occurrences) |
| W7 | 1.0 and 8.0 as needed | D11 (SystemSetting rows) |
| W8 | Every configuration process | ConfigurationAuditLog (written on every deactivation and every soft delete) |

**Stores never touched:** D7, D8, D12. No configuration process invokes 5.0, 6.0, 7.0, 9.0, 10.0, or 11.0.

The only write to an operational store is 8.0's insertion of event-sourced ServiceOccurrence rows into D3.

### 9.3 Invariants

**Rule neutrality.** No configuration process creates a link between Role and Duty except through a DutyRule row written by 2.0.

**Deactivation, not deletion.** Configuration entities whose contracts define lifecycle state are never removed.

**No backward propagation.** Editing any configuration row affects future use only.

**Immediate activation.** Created entities are active from creation, where the entity has lifecycle state.

**Name requirements.** Names are required where the schema declares them; they need not be unique.

**Referential integrity.** Every foreign key references an existing row.

**Schedule uniqueness.** An active ServiceSchedule is uniquely identified by (ServiceDefID, TimeSlotID).

**OwningBranch enforcement point.** Enforced when a ServiceSchedule is created or edited.

**Event-sourced occurrence creation.** When a ProgramItem links to a ServiceDefinition, an event-sourced ServiceOccurrence is written with GeneratedBy = Administrator and CreatedBy = the acting admin. This is the only operational write the configuration layer performs.

**No evaluation.** No configuration process evaluates any rule, threshold, or criterion.

**No orchestration.** No configuration process invokes any other process.

**Authorization upstream.** Configuration processes assume the caller has passed the applicable PermissionTier check.

**Lookup vocabulary.** D10 holds administrator-defined vocabulary.

### 9.4 Indexes

**Required:**

- ServiceSchedule (ServiceDefID, TimeSlotID) — UNIQUE, restricted to active rows.
- Foreign-key indexes on all FK columns.

**Active-row indexing strategy:** where supported by the target database, a partial or filtered index on active rows may be used for entities whose reads are consistently activity-filtered.

**DutyRule indexing:** a composite index on (DutyID, TierOrder) may be appropriate for 5.2's access pattern.

**Eligibility indexing:** the natural access pattern is by (MemberID, DutyID).

### 9.5 Explicitly Open Implementation Choices

- Physical representation of lifecycle state.
- Physical structure of Branch.Location.
- Free-text vs. lookup for MembershipStage, Event.Type, IdentifierHistory.Type.
- CriteriaValue validation timing.
- PermissionTier capability matrix — deferred.
- Deactivation audit columns.
- SystemSetting keys not yet defined.
- Active-row indexing strategy.
- Eligibility and DutyRule composite index shapes.
- Physical column types.
- Unique-constraint expression across database engines.

### 9.6 Attendance Register Scope

The attendance register has two independent purposes. It is a record of presence, and it is a signal that attendance-driven rules may read. The two purposes are decoupled: the register may be on for record-keeping while being off for rostering, or the reverse.

The register's on/off state is configured at three scopes:

- **Global.** A System Setting names the default state.
- **Per-event.** `Event.UsesAttendanceRegister`. Null means no override.
- **Per-branch.** `Branch.UsesAttendanceRegister`. Null means no override.

The resolution order is: event override, then branch override, then global. The most specific scope in effect wins. An occurrence belongs to either an event or a branch and time slot. If the occurrence is event-sourced, the event's override applies first. If the occurrence is schedule-sourced, the branch's override applies first. In each case the global setting is the fallback when no override exists.

When the effective scope is off, the attendance register is not consulted. The `Attendance Rule` engine does not apply absence-based rules to members whose effective scope is off. Manual register marking is disabled for that scope. The `Branch-Attendance Recency` Duty Rule criterion is skipped for occurrences at that scope, as stated in §10.1.5. Readmission-based rules may still apply, because readmissions are recorded independently of the register.

The register state may be changed at any time. A change takes effect on the next process run. It does not rewrite history.

**Source:** System Design Specification §9.6, §10.1.5, §13.

### 9.7 Configuration Lifecycle

Every configuration entity carries both `IsActive` and `IsDeleted`. Together they express four reachable states.

| State | IsActive | IsDeleted | Meaning |
| --- | --- | --- | --- |
| Active | true | false | Offered for new use. |
| Deactivated | false | false | Paused. Visible in an inactive view. Reversible. |
| Soft-deleted | false | true | Hidden from every normal view. Not reversible through normal operations. |
| Forbidden | true | true | Not permitted. A check constraint forbids it. |

Deactivation sets `IsActive = false`. The entity is not offered for new use, existing references are unaffected, and reactivation is a single admin action.

Soft delete sets `IsDeleted = true` and implicitly `IsActive = false`. The entity is hidden from all normal views. The reference fallback and cascade rules stated below are applied. Reactivation is not available through normal operations.

Every deactivation and every soft delete presents a consequences preview, requires acknowledgement, requires approval from an admin of a configured permission tier, and writes a `Configuration Audit Log` row. The reason is required for soft delete and optional for deactivate by default. The requirement and the approval tier are configured per entity type in `Entity Deletion Policy`.

On soft delete, the following reference handling applies:

| Entity | On soft delete |
| --- | --- |
| Role | Blocked if this is the default Role. Otherwise, members holding the Role fall back to the default Role. Duty Rules whose CriteriaType = Role and whose CriteriaValue names this Role are deleted. |
| Duty | Blocked if any Service Definition Duty or Event Duty row references the Duty. Otherwise the referencing rows are removed. Existing Roster Assignment rows referencing the Duty are preserved as historical records. |
| Member Status | Blocked if this is the default Member Status. Otherwise, members with this status fall back to the default. |
| Branch | Blocked if any Member is assigned to the Branch. Otherwise the referencing Branch Time Slot rows are removed. Existing Event HostBranchID and Event Branch rows referencing the Branch are preserved. |
| Branch Time Slot | Blocked if any Service Schedule references the Time Slot. Otherwise the referencing Service Schedule rows are removed if they were already inactive. |
| Service Definition | Blocked if any Service Schedule, Program Item, Event Duty, or Duty Rule references the Service Definition. |
| Service Definition Duty | Removed if the Service Definition is removed; not soft-deleted independently. |
| Service Schedule | Existing Service Occurrence rows are preserved. The schedule stops being eligible for future materialization. |
| Attribute Type | Member Attribute Value rows for this attribute are removed. Duty Rules whose CriteriaType = Member Attribute and whose CriteriaValue names this attribute are deleted. |
| Event | Referencing Program and Event Duty rows are removed. Existing event-sourced Service Occurrence rows are preserved. |
| Program | Referencing Program Item rows are removed. |
| Program Item | Referencing Event Duty rows are removed. |
| Event Duty | Removed; not soft-deleted independently. |
| Outcome State | Blocked if any Service Occurrence or System Setting references the state. |
| Assignment Status | Blocked if any Roster Assignment or System Setting references the status. |
| Time Of Day | Blocked if any Branch Time Slot references the Time Of Day. |
| Service Type | Blocked if any Service Definition or Service Occurrence references the Service Type. |
| Permission Tier | Blocked if any Admin or Entity Deletion Policy references the tier. |
| Capability | Removed; not soft-deleted independently, because nothing references it. |
| Entity Deletion Policy | Removed; not soft-deleted independently. |
| Notification Subscription | Removed; not soft-deleted independently. |

Configuration changes — including deactivation and soft delete — take effect on the next process run. They do not rewrite history. A Roster Assignment, once created, is not re-evaluated because a configuration row it was generated under was later changed or deleted. An Attendance Record names a member, not a role; deleting a role does not touch it.

The reason for the change is that the congregation manages configuration by a board of admins. Configuration actions are consequential, and the system must record who did them, why, and with what approval, while permitting the admin to remove what is no longer wanted without destroying history.

**Source:** System Design Specification §4, §9.3.

### 9.8 Transaction boundaries

Every operation that writes more than one row has an atomic boundary. The specification names which writes form an atomic unit. The mechanism — a single `SaveChanges`, an explicit database transaction, or an equivalent — is implementation choice.

- **5.0 per assignment.** The `RosterAssignment` insertion is the atomic persisted operation. The subsequent 9.0 invocation is outside that atomic boundary and cannot cause the assignment transaction to roll back.
- **7.0 respond.** The `AssignmentStatusID` update and, when the transition is terminal, the replacement `RosterAssignment` insertion are one atomic unit. The subsequent 9.0 invocation is outside it.
- **7.0 sweep.** Each assignment's status transition is its own atomic unit. The sweep does not wrap all transitions in a single transaction.
- **9.0 authority sweep.** The `NotifiedAt` update is atomic with respect to the audit row. Dispatch is outside the database atomic boundary.
- **10.0 per occurrence.** The `FillStatusID` write is atomic per occurrence.
- **11.0 per occurrence.** Each `ServiceOccurrence` insertion is atomic per row. A failed run leaves the rows created so far; a subsequent run resumes.
- **12.0 manual assignment.** The `RosterAssignment` insertion is the atomic persisted operation. The conditional 9.0 invocation is outside it.
- **13.0 per rule per member.** Each outcome application is its own atomic unit. Two rules on one member are two atomic units, applied sequentially per §13.6.
- **Configuration soft delete.** The cascade — fallback updates, dependent row removals, and the audit write — is one atomic unit.

### 9.9 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §10 — Generate Assignment.
- §11 — Manage Confirmation.
- §12 — Evaluate Fill Status.
- §13 — Record Attendance.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model. Note: §16 distinguishes invocation from triggering. Configuration processes do not invoke 9.0; the authority-notification path is a scheduled trigger of 9.0, not an invocation.
- §17 — Create Manual Assignment. Not a configuration-layer process, but listed here because it uses the same Eligibility and DutyRule modules this layer configures.

*End of §9. This section derives from the locked contracts of 1.0, 2.0, 3.0, 4.0, and 8.0, and from the two decisions locked immediately prior to §8 (D1 schedule uniqueness, D2 OutcomeState mapping).*

---

## 10. Generate Assignment (5.0) — Derived Consequences

*This appendix derives, from the locked 5.0 contract, the concrete schema behavior, data flows, invariants, and indexing the process requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §10.5.*

### 10.0 Process Boundary

5.0 Generate Assignment is the process that fills the required duty slots on a ServiceOccurrence by producing RosterAssignment rows. It reads configuration, reads member and eligibility data, reads existing assignments to top up rather than replace, and writes RosterAssignment.

It is one of the two processes in the system that invoke another process directly (the other is 7.0). When 5.0 creates a new RosterAssignment, it invokes 9.0 to dispatch the assignment notice.

**At a glance:**

- **Trigger:** scheduled run; manual administrator action (specific occurrence or date range); never invoked by 11.0.
- **Reads:** D2, D3, D4, D6, D7, and D8 only when a Branch-Attendance Recency criterion is present.
- **Writes:** D7 RosterAssignment.
- **Invokes:** 9.0 Dispatch Notification, on new automatic assignment creation.
- **Does not:** create occurrences, remove or modify existing assignments, send notifications directly, evaluate fill status, trigger downstream processes.

### 10.1 Derived Schema Behavior

#### 10.1.1 RosterAssignment — columns populated by 5.0

| Column | Value written by 5.0 |
| --- | --- |
| AssignmentID | System-generated |
| MemberID | The selected candidate |
| DutyID | The duty being filled |
| OccurrenceID | The occurrence being filled |
| AssignmentStatusID | NULL — 7.0 owns the lifecycle from this point |
| ApprovedBy | NULL |
| AssignmentSource | Automatic |
| AssignedBy | NULL |
| CreatedAt | The moment of insertion |

#### 10.1.2 Availability

A member is available for a duty on an occurrence if and only if they do not have a **non-terminal** RosterAssignment on that OccurrenceID. Terminal assignments do not block availability, because they represent slots the member no longer occupies.

This uses AssignmentStatus.IsTerminal, the same boolean used by 7.0's re-resolution logic and 10.0's capacity calculation.

**Source of the rule.** The occurrence-level exclusion is a fixed default the system applies. It is not a configurable rule in the current specification, and it is not a rule the assignment process is free to alter. The process applies it because the specification states it as the mechanical default.

**Specification gap.** A future revision may define a configurable override that permits a member to hold more than one duty on the same occurrence when the administrator configures it — for example, where two duties are carried by the same role and only one such member is available. That override would be expressed through the Duty Rule criteria vocabulary. **The current criteria vocabulary (see §5) does not include a criterion that evaluates a member's existing assignments on the same occurrence, and therefore cannot currently express this exception.** No such criterion is introduced by this specification. The absence is recorded here so that any future addition is a deliberate specification change, not a silent invention.

#### 10.1.3 Capacity — the slot-count rule

For each (OccurrenceID, DutyID) pair, the effective slot count is:

- The RequiredSlotCount from ServiceDefinitionDuty for the duty, overridden by ServiceOccurrenceDuty.RequiredSlotCount if an override row exists with Action = Added.
- If a ServiceOccurrenceDuty row exists with Action = Removed, the duty is not required for that occurrence, and no slots are filled.

5.0 counts existing non-terminal assignments for the pair and fills up to the remaining count. Terminal assignments do not count toward the total.

#### 10.1.4 Eligibility resolution

For each (Member, Duty) pair, 5.0 determines eligibility by resolving the applicable Eligibility row. The rule 5.0 uses:

> The applicable row is the one with the latest GrantedDate that is not in the future and whose RevokedDate is either null or in the future. If multiple such rows exist for the same (MemberID, DutyID), the one with the greatest EligibilityID is used as the deterministic tiebreaker. If the resolved row has a RevokedDate set to a past date, the member is not eligible. If no row exists at all, the member is not eligible.

Three properties follow from the rule:

- A grant with a future GrantedDate does not make the member eligible yet.
- A grant with a future RevokedDate is still in effect.
- The tiebreaker (greatest EligibilityID) is the rule 5.0 applies, not a rule the schema enforces.

#### 10.1.5 Duty Rule evaluation

Tiers are evaluated in ascending TierOrder. For each tier, all rules sharing that TierOrder are combined with AND. The first tier that produces at least one eligible, available candidate supplies the candidate set. If no Duty Rules exist for a duty, every eligible member enters as an equal candidate.

Criteria are evaluated per CriteriaType:

| CriteriaType | Evaluation |
| --- | --- |
| Role | Member's RoleID matches the rule's CriteriaValue (resolved by name) |
| Gender | Member's Gender matches |
| Age Range | Computed from DateOfBirth and today's date, within the range |
| Tenure | Computed from JoinDate and today's date, meets the threshold |
| Membership Stage | Member's MembershipStage matches |
| Branch-Attendance Recency | Member has an AttendanceRecord at the specified branch within the configured window. The rule is skipped for occurrences whose effective attendance register scope is off. See §9.6. |
| Eligibility Flag | Member has a current Eligibility grant for the referenced duty |
| Member Attribute | Member holds the named Attribute Type with the required value in their Member Attribute Value rows |
| Youth | Member's age, computed from DateOfBirth against the configured YouthAgeMin and YouthAgeMax settings, falls within the configured range |

**Rule scope.** A Duty Rule may carry a null ServiceDefID, in which case it applies to the duty wherever the duty appears. A rule may carry a ServiceDefID, in which case it applies only where the duty is required by that specific Service Definition, and it overrides the duty-global rules entirely for that (Service, Duty) pair. If no per-service rules exist for a pair, the duty-global rules apply. If neither exists, every eligible member is an equal candidate.

##### Criteria value grammars

Each criterion's `CriteriaValue` has a fixed grammar. The grammar is part of the specification, not configuration: the administrator chooses the value, but cannot redefine what constitutes a valid value.

Before evaluation, the value is trimmed of leading and trailing whitespace. Whitespace inside the value is handled by the grammar: it is rejected where the grammar does not permit it, and preserved where the value is a string whose content may legitimately contain spaces.

| Criterion | Grammar | Matching |
| --- | --- | --- |
| Role | A role name, case-sensitive | Resolved against the Role table by exact name; matched by RoleID |
| Gender | A gender value, case-sensitive | Exact match against Member.Gender |
| Membership Stage | A membership stage value, case-sensitive | Exact match against Member.MembershipStage |
| Age Range | `min-max`, both non-negative integers, inclusive, no whitespace | Computed from Member.DateOfBirth against today's date. See the Age calculation subsection. |
| Tenure | A non-negative integer, days, no whitespace | Days since Member.JoinDate, >= the value |
| Branch-Attendance Recency | A non-negative integer, days, no whitespace | The member has an AttendanceRecord at the occurrence's branch within the last N days |
| Eligibility Flag | A positive integer, the DutyID, no whitespace | The member has a current Eligibility grant for the referenced Duty |
| Member Attribute | `AttributeTypeName=RequiredValue` | The AttributeType is resolved by exact case-sensitive name. The member must hold a MemberAttributeValue for that type whose Value equals RequiredValue exactly. The RequiredValue may contain spaces; the syntax around the `=` does not permit spaces. |
| Youth | Empty | Computed from Member.DateOfBirth against YouthAgeMin and YouthAgeMax |

**Malformed values.** A rule whose `CriteriaValue` does not match its grammar produces no candidates. It is not an error and does not fail the run. Evaluation continues. If a tier contains no candidate because of a malformed rule, the engine falls through to the next tier as it would for any tier producing no candidate.

**Deprecated.** The settings `AgeRangeMin`, `AgeRangeMax`, and `TenureThresholdDays` are removed. The specification no longer falls back to any default for the Age Range or Tenure criteria. The criterion value is the sole source.

##### Age calculation

The Age Range and Youth criteria share one age calculation. Age is the number of completed birthdays as of the current calendar date in the configured ApplicationTimeZone. A member satisfies the Youth criterion when `YouthAgeMin <= age <= YouthAgeMax`, inclusive at both boundaries. A member satisfies an Age Range criterion when the range's `min` is less than or equal to the age and the range's `max` is greater than or equal to the age, both inclusive. There is one age function; the two criteria do not compute age differently.

Reserved criteria types (Acceptance Rate, Duties Carried, Days Since Last Assignment) are not evaluated until a future specification revision activates them. If a tier consists entirely of rules whose criteria types are reserved, the tier is treated as producing no candidates.

#### 10.1.6 Selection within a tier

When the first qualifying tier produces more candidates than remaining slots for a duty, 5.0 selects from the candidate set in ascending `MemberID` order. The lowest `MemberID` in the candidate set is assigned first, then the next, until the remaining slots are filled. The ordering is fixed; it is not configurable. No rotation, load balancing, recency ordering, or other implicit fairness is applied. Random selection is not used, because it would make the outcome non-deterministic.

Administrators who want a fairness or preference property expressed configure it through Duty Rule criteria — for example, using `Tenure` to prefer longer-standing members. The tie-breaker provides deterministic mechanical selection; it does not express policy.

#### 10.1.7 Re-entry and uniqueness

RosterAssignment is unique on (MemberID, DutyID, OccurrenceID). This is the database-level guard against duplicate member/duty/occurrence records, regardless of source.

5.0 is fully re-entrant. An interrupted run resumes on the next run.

#### 10.1.8 Interaction with 9.0

When 5.0 creates a new automatic assignment, it invokes 9.0 with that assignment as input. 9.0 sends an assignment notice to the member. 5.0 does not wait for the notification result; the invocation is fire-and-forget. A failure of the invocation itself — a thrown exception or a timeout — does not roll back the assignment. 5.0 catches the failure, logs it, and continues to the next assignment. The assignment remains committed. The run is not marked as a failure because a notification was not delivered. The result counters reflect assignments created, not notifications delivered. 5.0 does not retry the invocation; retry, if ever introduced, is a feature of 9.0, not of its callers.

### 10.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D3 ServiceOccurrence | The occurrence being filled |
| R2 | D3 ServiceDefinitionDuty | Inherited duty list and slot counts |
| R3 | D3 ServiceOccurrenceDuty | Per-occurrence overrides |
| R4 | D4 Member | Candidate member data |
| R5 | D6 Eligibility | Candidacy gate |
| R6 | D2 DutyRule | Tier-based ranking |
| R7 | D7 RosterAssignment | Existing assignments |
| R8 | D8 AttendanceRecord | Only when a Branch-Attendance Recency criterion is present |
| R9 | D3 BranchTimeSlot | For time-related criteria evaluation |

**Writes:**

| # | Target | Fields |
| --- | --- | --- |
| W1 | D7 RosterAssignment | MemberID, DutyID, OccurrenceID, AssignmentStatusID = NULL, ApprovedBy = NULL, AssignmentSource = Automatic, AssignedBy = NULL, CreatedAt |

**Invocations:**

| # | Process invoked | Trigger |
| --- | --- | --- |
| I1 | 9.0 Dispatch Notification | On each new automatic assignment created |

**Stores never touched:** D1, D5, D9, D10, D11, D12. Never modifies ServiceOccurrence, existing RosterAssignment rows, or ServiceOccurrenceDuty.

### 10.3 Invariants

- Eligibility gates candidacy; Duty Rule only ranks.
- Tiers are evaluated in ascending TierOrder.
- Same-tier rules are ANDed.
- No tier → every eligible member is an equal candidate.
- When a tier produces more candidates than slots, the tie-breaker is ascending MemberID. The rule is fixed and is not configurable.
- No candidate in any tier → slot remains unfilled.
- Fewer candidates than slots → remaining slots unfilled.
- 5.0 never removes or modifies an existing assignment.
- 5.0 does not assign a member to more than one duty on the same occurrence.
- Availability respects terminal statuses.
- (MemberID, DutyID, OccurrenceID) is unique in RosterAssignment.
- 5.0 is fully re-entrant.
- AssignmentStatusID is null at creation.
- AssignmentSource = Automatic, AssignedBy = null for automatic assignments.
- 5.0 never writes FillStatusID.
- 5.0 never modifies ServiceOccurrence.
- 5.0 invokes only 9.0.
- 5.0 is independent of 11.0.

### 10.4 Indexes

**Required:**

- RosterAssignment (MemberID, DutyID, OccurrenceID) — UNIQUE. The integrity guard.
- RosterAssignment (OccurrenceID, DutyID) — non-unique. Serves the capacity-count query and availability check.
- Eligibility (MemberID, DutyID) — composite, non-unique.
- DutyRule (DutyID, TierOrder) — composite, non-unique.

**Implementation-dependent:** the composite index shapes for Eligibility and DutyRule. Physical forms depend on the target database and actual query plan.

### 10.5 Explicitly Open Implementation Choices

- Eligibility resolution query shape.
- Whether 5.0's scheduled and manual runs share a code path.
- Batching strategy for filling slots across duties within an occurrence.
- The concurrency control mechanism for preserving slot capacity. The required property is stated in §10.3; the mechanism is not prescribed.

### 10.6 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §9 — Configuration layer.
- §11 — Manage Confirmation.
- §12 — Evaluate Fill Status.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model.

*End of §10. This section derives from the locked 5.0 contract and does not extend it.*

---

## 11. Manage Confirmation (7.0) — Derived Consequences

*This appendix derives, from the locked 7.0 contract, the concrete schema behavior, data flows, invariants, and indexing the process requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §11.5.*

### 11.0 Process Boundary

7.0 Manage Confirmation is the process that tracks the response lifecycle of each RosterAssignment. It transitions the assignment's AssignmentStatusID from NULL (as written by 5.0) through the admin-defined statuses, and on decline or timeout it re-resolves only the affected slot.

It is one of the two processes in the system that invoke another process directly (the other is 5.0). When 7.0 creates a replacement assignment after a decline or timeout, it invokes 9.0 to dispatch the assignment notice.

**At a glance:**

- **Trigger:** member response (confirm or decline); scheduled check for timeouts.
- **Reads:** D7 RosterAssignment, D10 AssignmentStatus, D11 SystemSetting, D4 Member.
- **Writes:** D7 RosterAssignment — updates AssignmentStatusID; inserts a replacement assignment on decline or timeout.
- **Invokes:** 9.0 Dispatch Notification, on replacement assignment creation.
- **Does not:** create the initial assignment (5.0 does), evaluate fill status (10.0 does), record attendance (6.0 does), send notifications directly.

### 11.1 Derived Schema Behavior

#### 11.1.1 RosterAssignment — fields updated by 7.0

On a member response or a timeout detection:

| Column | Action by 7.0 |
| --- | --- |
| AssignmentStatusID | Updated to the admin-designated status for the event (confirmed, declined, or timed out) |
| All other columns | Unchanged |

On a decline or timeout that triggers re-resolution, a **new** RosterAssignment row is inserted for the replacement:

| Column | Value |
| --- | --- |
| MemberID | The newly selected candidate |
| DutyID | The same duty as the vacant slot |
| OccurrenceID | The same occurrence |
| AssignmentStatusID | NULL — the lifecycle restarts for the replacement |
| ApprovedBy | NULL |
| AssignmentSource | Automatic |
| AssignedBy | NULL |
| CreatedAt | The moment of insertion |

7.0 does not create manual assignments. Manual assignment, if it exists as an operational action, belongs to a separate admin operation and writes AssignmentSource = Manual with AssignedBy = <AdminID>.

#### 11.1.2 Terminal vs non-terminal

AssignmentStatus.IsTerminal distinguishes statuses that occupy a required slot from statuses that leave the slot vacant. This is the same boolean used by 5.0's availability rule and by 10.0's capacity calculation. IsTerminal is a capacity concept, not a lifecycle-impermanence concept — a terminal assignment's row remains, and the member may rejoin a slot through a new assignment.

A status marked IsTerminal = true does not count toward the effective required slot count.

#### 11.1.3 Status transitions

The system does not enforce a state machine keyed on status identity. It does not know what any AssignmentStatus name means. The only lifecycle property it reads is `AssignmentStatus.IsTerminal`. The permitted transitions are therefore expressed in terms of terminality, not status names.

| From | To | Permitted | Condition |
| --- | --- | --- | --- |
| NULL | any | Yes | Subject to the applicable process operation. 5.0 creates with NULL; 7.0 owns every transition after that. |
| non-terminal | non-terminal | Yes | Unconditional. |
| non-terminal | terminal | Yes | Unconditional. |
| terminal | terminal | Yes | Unconditional. A terminal-to-terminal move vacates nothing, so no capacity check applies. |
| terminal | non-terminal | Conditional | Permitted only if the resulting non-terminal assignment count for the affected (OccurrenceID, DutyID) does not exceed the effective required slot count. If it would, the transition is rejected. |

The `terminal → non-terminal` guard is the capacity invariant stated in §11.3. The system does not refuse the transition because it names a status that should not follow the current one; it refuses it because the capacity invariant would be violated.

An administrator may configure any status names and any `IsTerminal` values. The system's behaviour follows from the flag, not from the name. Changing a status's `IsTerminal` value takes effect on the next process run and does not rewrite existing assignments.

#### 11.1.4 Status lifecycle ownership

5.0 creates automatic assignments with AssignmentStatusID = NULL. 7.0 owns the status transitions for assignments after creation. Other processes — for example, a manual assignment operation — may also create assignments with AssignmentStatusID = NULL, subject to their own contracts.

#### 11.1.5 Initial status designation

7.0 transitions an assignment from NULL to the admin-designated initial status. This status is identified via `SystemSetting.InitialAssignmentStatusID`, a required setting. If the setting is unset or invalid, 7.0 refuses to run and surfaces a configuration error.

7.0 resolves three target statuses from settings, one per operation:

| Operation | Setting | Applies to |
| --- | --- | --- |
| Confirm | `InitialAssignmentStatusID` | A NULL assignment |
| Decline | `DeclinedStatusID` | A NULL or non-terminal assignment |
| Timeout sweep | `TimedOutStatusID` | A NULL assignment past the timeout |

All three settings are required. The system does not name or interpret any AssignmentStatus; the settings point at admin-created rows. Changing any setting takes effect on the next 7.0 run and does not rewrite existing assignments.

#### 11.1.6 Timeout measurement

An assignment becomes eligible for timeout when its elapsed age reaches the configured ConfirmationTimeoutHours:
now - CreatedAt >= ConfirmationTimeoutHours

The timeout check runs as a scheduled part of 7.0.

If ConfirmationTimeoutHours is unset and Required = false, no timeout occurs. If Required = true and unset, 7.0 refuses to run and surfaces a configuration error.

**Past-occurrence guard.** The timeout sweep applies a fixed system rule to guard against acting on a service that has already happened. The timeout transition is recorded for any eligible assignment, regardless of the occurrence's date. However, if the occurrence's date is before today in the configured `ApplicationTimeZone`, the 7.0 process **will not seek a replacement assignment and will not send a notification.**

This is a fixed rule, resolved via `ApplicationTimeZone`. The administrator configures `ConfirmationTimeoutHours`; the specification owns the past-occurrence exclusion. This rule adds no new setting.

#### 11.1.7 Re-resolution on decline or timeout

When a member declines or an assignment times out:

1. The assignment's AssignmentStatusID transitions to the admin-designated target status. On the decline path, the target is `SystemSetting.DeclinedStatusID`; the decline operation applies to a NULL or a non-terminal assignment. On the timeout sweep path, the target is `SystemSetting.TimedOutStatusID`; the timeout sweep applies only to assignments whose AssignmentStatusID is NULL and whose elapsed age meets the configured timeout. Both settings are required; if either is absent, 7.0 refuses the corresponding operation and surfaces a configuration error.
2. The status must have IsTerminal = true for the slot to be considered vacant; if the admin marks a decline status as non-terminal, the slot remains occupied and 7.0 does not re-resolve.
3. 7.0 runs the same candidate selection logic 5.0 uses, scoped to the affected (OccurrenceID, DutyID) slot only.
4. **Any member who has previously declined or timed out for this specific (OccurrenceID, DutyID) slot is excluded from the replacement candidate set.** The exclusion is cumulative: it is not limited to the member whose assignment just became terminal. A member who declined this slot earlier in the occurrence's lifecycle — and was replaced — remains excluded from all subsequent replacement attempts for that slot. Their terminal assignment does not exclude them under the general availability rule; this is an additional re-resolution rule specific to 7.0.
5. If a candidate is found, a replacement RosterAssignment row is inserted with AssignmentStatusID = NULL, AssignmentSource = Automatic, AssignedBy = NULL, and 7.0 invokes 9.0 for the notification.
6. If no candidate is found, the slot remains vacant. No fallback.

The declining or timed-out row is never modified beyond the status transition. Its history is preserved. The cumulative exclusion is derived from those preserved rows: any member holding a terminal assignment on this (OccurrenceID, DutyID) is excluded from the replacement candidate set, permanently for this slot. No new field or table is required — the Roster Assignment rows already carry the information needed.

#### 11.1.8 Interaction with 9.0

7.0 invokes 9.0 only when it creates a replacement assignment. It does not invoke 9.0 on status changes of existing assignments. The invocation is fire-and-forget. 9.0's failure does not roll back the replacement assignment.

### 11.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D7 RosterAssignment | The assignment being processed |
| R2 | D10 AssignmentStatus | Resolve designated statuses; check IsTerminal |
| R3 | D11 SystemSetting | InitialAssignmentStatusID, ConfirmationTimeoutHours |
| R4 | D4 Member | Member context for confirm/decline |
| R5 | D2 DutyRule | Only during re-resolution |
| R6 | D6 Eligibility | Only during re-resolution |
| R7 | D4, D3 | Only during re-resolution |
| R8 | D8 AttendanceRecord | Only during re-resolution, if Branch-Attendance Recency is present |

**Writes:**

| # | Target | Action |
| --- | --- | --- |
| W1 | D7 RosterAssignment | Update AssignmentStatusID on the existing row |
| W2 | D7 RosterAssignment | Insert a replacement row |

**Invocations:**

| # | Process invoked | Trigger |
| --- | --- | --- |
| I1 | 9.0 Dispatch Notification | On each replacement assignment created |

**Stores never touched:** D1, D5, D9. Never modifies ServiceOccurrence, FillStatusID, ServiceOccurrenceDuty, or AttendanceRecord.

### 11.3 Invariants

- Status lifecycle ownership: 5.0 creates with NULL; 7.0 owns transitions.
- Status transitions are expressed in terms of terminality, not status identity. The permitted-transition matrix is stated in §11.1.3. `NULL → any`, `non-terminal → non-terminal`, `non-terminal → terminal`, and `terminal → terminal` are unconditional. `terminal → non-terminal` is permitted only when the resulting non-terminal assignment count for the affected (OccurrenceID, DutyID) does not exceed the effective required slot count. This is a lifecycle invariant expressed through `AssignmentStatus.IsTerminal`; no status name is examined.
- Timeout rule: `now - CreatedAt >= ConfirmationTimeoutHours`.
- Past-occurrence guard: the timeout transition is recorded, but no replacement is sought and no notification is sent when the occurrence's date is before today in the configured ApplicationTimeZone. This is a fixed rule, resolved via ApplicationTimeZone. It adds no setting.
- Required-setting failure: missing InitialAssignmentStatusID or required ConfirmationTimeoutHours → 7.0 refuses to run.
- Terminal statuses leave slots vacant.
- Replacement is additive; the declined/timed-out row is not modified beyond the status transition.
- History preserved.
- Re-resolution scoped to the affected slot only.
- Members who have previously declined or timed out on this (OccurrenceID, DutyID) slot are cumulatively excluded from all subsequent replacement candidate sets for that slot.
- No automatic re-resolution when no candidate is found.
- 9.0 invoked only on replacement creation.
- Fire-and-forget notification.
- No downstream trigger besides 9.0.
- No fill-status evaluation.
- No occurrence modification.
- No attendance record.
- **Slot capacity is preserved under concurrency.** The concurrent writers of `RosterAssignment` rows are 5.0 Generate Assignment, 7.0 Manage Confirmation, and 12.0 Create Manual Assignment. Multiple concurrent executions of any of these processes may contend. The required property is: for every (OccurrenceID, DutyID) pair, the committed number of non-terminal `RosterAssignment` rows never exceeds the effective required slot count for that pair, under any interleaving of concurrent writers. The property is evaluated against the committed resulting state, not against every transient intermediate state during transaction execution. The mechanism — row-level locking, serializable isolation, an advisory lock, or an application-level mutex — is an implementation choice, as listed in §10.5. The specification requires that the property hold; it does not prescribe the mechanism.

### 11.4 Indexes

**Candidate access patterns:**

- RosterAssignment (CreatedAt) — natural access pattern for timeout detection.
- RosterAssignment (OccurrenceID, DutyID, AssignmentStatusID) — natural access pattern for affected-slot resolution.

**Implementation-dependent:** whether either index is created, and its exact physical shape.

### 11.5 Explicitly Open Implementation Choices

- Whether status transitions are logged.
- Timeout check cadence.
- Candidate ordering during re-resolution.
- Concurrency control mechanism — row-level locking, serializable isolation, or application-level mutex. The invariant to preserve: a duty's non-terminal assignment count does not exceed its effective required slot count.
- Whether 7.0 transitions a fresh assignment from NULL to the initial status as part of a scheduled sweep, or on demand.
- Physical shape of the timeout index.

### 11.6 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §9 — Configuration layer.
- §10 — Generate Assignment.
- §12 — Evaluate Fill Status.
- §13 — Record Attendance.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model.

*End of §11. This section derives from the locked 7.0 contract and does not extend it.*

---

## 12. Evaluate Fill Status (10.0) — Derived Consequences

*This appendix derives, from the locked 10.0 contract, the concrete schema behavior, data flows, invariants, and indexing the process requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §12.5.*

### 12.0 Process Boundary

10.0 Evaluate Fill Status is the process that computes each occurrence's fill state and writes it to ServiceOccurrence.FillStatusID. It reads the occurrence's assignments, the occurrence's required duty list, the three OutcomeState mapping settings, and (optionally) the cancelled-state designation.

It does not create assignments, does not modify assignments, does not modify any occurrence field other than FillStatusID, and does not invoke any other process.

**At a glance:**

- **Trigger:** periodic sweep. 10.0 observes assignment changes on its own scheduled run. No process invokes 10.0; the phrase "after assignment changes" describes the condition under which 10.0's work becomes necessary, not a process-to-process call.
- **Reads:** D7 RosterAssignment, D3 ServiceDefinitionDuty, D3 ServiceOccurrenceDuty, D10 OutcomeState, D11 SystemSetting.
- **Writes:** D3 ServiceOccurrence.FillStatusID.
- **Does not:** invoke any process; modify any field other than FillStatusID; assign cancellation.

### 12.1 Derived Schema Behavior

#### 12.1.1 FillStatusID — the only field written

10.0 writes only ServiceOccurrence.FillStatusID. No other column of ServiceOccurrence is touched. No other store is written.

#### 12.1.2 The mechanical classification

For each occurrence, 10.0 computes the count of **active** assignments (non-null, non-terminal AssignmentStatusID) across all required duties, and compares it against the **effective required count** (the sum of RequiredSlotCount for each duty, after applying ServiceOccurrenceDuty overrides).

Three mechanical conditions result:

| Condition | Mechanical meaning |
| --- | --- |
| Unfilled | Zero active assignments across all required duties |
| Partially Filled | Active assignments exist, but fewer than the effective required count |
| Filled | Active assignments meet or exceed the effective required count |

**Zero-duty occurrence.** If the effective required count is zero (no required duties for the occurrence, or all duties removed via ServiceOccurrenceDuty overrides), the occurrence is mechanically classified as **Filled**, because the active assignment count (zero) meets the effective required count (zero). This is a consequence of the mechanical rule, not a special case. It is consistent with the locked zero-duty rule from the 11.0 contract.

The classification is mechanical. The names are admin-defined. The mapping between them is via SystemSetting.

#### 12.1.3 OutcomeState mapping — locked via SystemSetting

Three required settings map the three mechanical conditions to admin-defined OutcomeState entries:

- OutcomeStateUnfilledID — the OutcomeStateID to write when the condition is Unfilled
- OutcomeStatePartiallyFilledID — when Partially Filled
- OutcomeStateFilledID — when Filled

If any of the three is unset or invalid, 10.0 refuses to run entirely — it does not partially evaluate occurrences. The run fails with a configuration error.

#### 12.1.4 Cancellation exclusion — locked via SystemSetting

Cancellation is outside the three mechanical fill classifications. 10.0 does not assign the cancelled state and does not replace an existing cancelled FillStatusID.

The cancelled state is identified via SystemSetting.OutcomeStateCancelledID:

- Required = false — cancellation is optional in the operational model.
- Holds an OutcomeStateID.
- If configured, 10.0 skips any occurrence whose current FillStatusID equals that ID.
- If absent, no automatic cancellation exclusion applies and 10.0 evaluates every occurrence.
- 10.0 never writes the cancelled state.

#### 12.1.5 Idempotency

10.0 is idempotent. Running it multiple times on the same occurrence produces the same FillStatusID as long as the underlying assignments haven't changed.

#### 12.1.6 No downstream trigger

10.0 does not invoke any process. The FillStatusID it writes is read by admin dashboards and by any future process that consumes it. No notification is triggered by fill status changes.

### 12.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D3 ServiceOccurrence | The occurrence being evaluated, and its current FillStatusID |
| R2 | D3 ServiceDefinitionDuty | Inherited duty list and slot counts |
| R3 | D3 ServiceOccurrenceDuty | Per-occurrence overrides |
| R4 | D7 RosterAssignment | Active assignments per duty |
| R5 | D10 OutcomeState | Resolve the OutcomeStateID values |
| R6 | D11 SystemSetting | OutcomeStateUnfilledID, OutcomeStatePartiallyFilledID, OutcomeStateFilledID, OutcomeStateCancelledID (optional) |

**Writes:**

| # | Target | Fields |
| --- | --- | --- |
| W1 | D3 ServiceOccurrence | FillStatusID only |

**Invocations:** None. 10.0 does not invoke any process.

**Stores never touched:** D1, D2, D4, D5, D6, D8, D9, D12. Never modifies RosterAssignment, ServiceOccurrenceDuty, or any field of ServiceOccurrence other than FillStatusID.

### 12.3 Invariants

- Mechanical classification: Unfilled, Partially Filled, or Filled, based on active assignment counts and effective required counts.
- Active means non-null, non-terminal AssignmentStatusID.
- Effective required count includes ServiceOccurrenceDuty overrides.
- Zero-required-count occurrence is Filled.
- Required-setting failure: any of the three mechanical mapping settings unset or invalid → 10.0 refuses to run entirely. No partial evaluation.
- Cancellation exclusion is optional. If OutcomeStateCancelledID is configured, 10.0 skips occurrences whose FillStatusID equals that ID. If absent, no exclusion applies.
- 10.0 never writes cancellation.
- Idempotent.
- Single field written: ServiceOccurrence.FillStatusID.
- No downstream trigger.
- No notification.
- No assignment modification.
- No occurrence field other than FillStatusID.

### 12.4 Indexes

**Candidate access patterns:**

- RosterAssignment (OccurrenceID, DutyID) — natural access pattern for fill-state computation. The existing index from §10 covers this.
- A composite extension including AssignmentStatusID may be appropriate depending on the target database and observed query plans.
- ServiceOccurrence (FillStatusID) — natural access pattern for "which occurrences are unfilled" queries.

**Implementation-dependent:** whether any additional index beyond the existing (OccurrenceID, DutyID) is created.

### 12.5 Explicitly Open Implementation Choices

- Scheduled sweep cadence.
- Batch vs per-occurrence processing.
- Physical shape of the fill-state index.
- Whether 10.0's write updates FillStatusID even when the new value equals the old value.
- **Admin-facing distinction between a filled occurrence and a zero-duty occurrence.** Both classify mechanically as Filled (active assignments meet or exceed effective required count, which is 0 for a zero-duty occurrence). An admin-facing dashboard may label them differently — e.g. "Filled" versus "Filled (no duties required)" — so the two facts are distinguishable at a glance. This is a presentation concern, not a schema or process concern; the mechanical classification remains unchanged.

### 12.6 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §9 — Configuration layer.
- §10 — Generate Assignment.
- §11 — Manage Confirmation.
- §13 — Record Attendance.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model.

*End of §12. This section derives from the locked 10.0 contract and from the two decisions locked immediately prior to §11 (E1 timeout boundary, E2 cancellation designation).*

---

## 13. Record Attendance (6.0) — Derived Consequences

*This appendix derives, from the locked 6.0 contract, the concrete schema behavior, data flows, invariants, and indexing the process requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §13.5.*

### 13.0 Process Boundary

6.0 Record Attendance is the process that writes AttendanceRecord rows when a member's presence at an occurrence is registered. It is the narrowest process in the system — it reads two stores, writes one, and has no downstream consequences within the system's own logic.

Attendance is a raw fact. 6.0 does not interpret it, does not condition it on assignment, does not affect fill status, does not affect assignment status, and does not trigger any other process.

**At a glance:**

- **Trigger:** member action at an occurrence (NFC tap or configured channel); admin manual entry.
- **Reads:** D4 Member, D3 ServiceOccurrence.
- **Writes:** D8 AttendanceRecord.
- **Does not:** evaluate, interpret, notify, trigger, or condition on assignment.
- **Idempotent per (MemberID, OccurrenceID).**

### 13.1 Derived Schema Behavior

#### 13.1.1 AttendanceRecord — fields written by 6.0

| Column | Value |
| --- | --- |
| RecordID | System-generated |
| MemberID | The member whose presence is recorded |
| OccurrenceID | The occurrence at which presence is recorded |
| Timestamp | The moment the record is written |

No other columns. The row is the raw fact.

#### 13.1.2 Uniqueness — one recorded fact per member per occurrence

AttendanceRecord permits at most one recorded attendance fact for a given (MemberID, OccurrenceID) pair. The uniqueness constraint is the integrity guard. A subsequent tap for an existing pair is a no-op — it does not create a second row.

This makes 6.0 idempotent per (MemberID, OccurrenceID). Re-running the same tap produces no new row.

#### 13.1.3 Independence from assignment

6.0 does not check whether the member is assigned to the occurrence. A member may be rostered and attend, rostered and not attend, not rostered and attend, or not rostered and not attend. All four are valid states. Attendance is a fact; assignment is a plan.

The branch-agnostic attendance default is realized here: a member attending at a branch other than their home branch produces an attendance record against that occurrence, with no reference to Member.BranchID.

#### 13.1.4 Admin manual entry

An admin may add an attendance record directly. The row is identical to a tap-initiated one: same columns, same uniqueness rule. The schema has no field distinguishing manual entry from tap entry.

#### 13.1.5 No interpretation layer

6.0 does not compute attendance recency. It does not update any cache or derived field. The Branch-Attendance Recency criterion used by DutyRule evaluation (5.0) reads AttendanceRecord at evaluation time, not from any precomputed state.

### 13.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D4 Member | Validate the member identity |
| R2 | D3 ServiceOccurrence | Identify which occurrence the record applies to |

**Writes:**

| # | Target | Action |
| --- | --- | --- |
| W1 | D8 AttendanceRecord | Insert a new row on the first recorded attendance for a (MemberID, OccurrenceID) pair |

A subsequent tap for an existing pair is a no-op. The uniqueness constraint is the integrity guard.

**Invocations:** None. 6.0 does not invoke any process.

**Stores never touched:** D1, D2, D5, D6, D7, D9, D10, D11, D12. Never modifies ServiceOccurrence, FillStatusID, or any assignment.

### 13.3 Invariants

- Raw fact. Attendance is a fact of presence, not an interpretation.
- Uniqueness. AttendanceRecord permits at most one recorded attendance fact for a given (MemberID, OccurrenceID) pair.
- Idempotent per member per occurrence.
- Independence from assignment.
- Branch-agnostic.
- No attendance-window enforcement.
- No interpretation. 6.0 does not compute recency, streaks, or any derived value.
- No downstream trigger.
- No notification.
- No assignment or fill-status side effect.

### 13.4 Indexes

**Integrity requirement:**

- AttendanceRecord (MemberID, OccurrenceID) — UNIQUE.

**Candidate access patterns:**

- AttendanceRecord (OccurrenceID) — natural access pattern for "who attended this occurrence" queries.
- AttendanceRecord (MemberID, Timestamp) — natural access pattern for Branch-Attendance Recency evaluation and for member-facing attendance history.

**Implementation-dependent:** the physical shape of the (MemberID, Timestamp) index.

**Deliberately not indexed:** standalone Timestamp; additional index for Branch-Attendance Recency.

### 13.5 Explicitly Open Implementation Choices

- Whether manual admin entry is distinguished from tap entry.
- Attendance window enforcement — not performed by 6.0 or the schema.
- Whether attendance records are retained indefinitely.
- Physical shape of the (MemberID, Timestamp) index.

### 13.6 Attendance Rule Engine

The Attendance Rule engine is the process that reads Attendance Rules, evaluates each against the member population, and applies the configured outcomes. It is the thirteenth process in the system.

**Trigger.** Scheduled run. It also runs on demand when an admin invokes it. A scheduled run does not evaluate rules whose `TriggerType` is `Manual`; those rules fire only through the explicit administrative invocation.

**Reads:** `AttendanceRule`, `AttendanceRuleScope`, `Member`, `AttendanceRecord`, `Readmission`, `MemberStatus`, `SystemSetting` (the global register scope), `Branch.UsesAttendanceRegister`, `Event.UsesAttendanceRegister`.

**Writes:** `Member.MemberStatusID` when a `SetStatus` outcome fires. Notification dispatches when a `Notify` outcome fires. `ConfigurationAuditLog` is not written by this process; it is a configuration-lifecycle record. The engine does not write `Readmission`; readmissions are recorded by administrative action through the member-management process. A `Manual` rule is applied through a distinct administrative invocation that names exactly one rule and applies its outcome to every member matching its scope.

**Does not:** modify `AttendanceRecord`, invoke any other process, or modify any operational record other than `Member.MemberStatusID`. The readmission count is derived from `Readmission` rows and is not a stored field; the engine reads `Readmission` and never writes it.

For each enabled Attendance Rule, the process:

1. Resolves the rule's scope. The scope rows are ANDed, using the same criterion evaluation as Duty Rule.
2. Resolves the effective attendance register scope for each member under consideration.
3. If the register is off at the effective scope, skips the member for absence-based triggers.
4. Evaluates the trigger. `AbsenceDays` measures calendar days in the configured ApplicationTimeZone. The window is `[today - N, today]`, inclusive at both boundaries. The trigger fires when the member has no AttendanceRecord whose related ServiceOccurrence.Date falls within the window. The window is not measured in elapsed hours. The attendance timestamp is not used for the absence-window calculation; the occurrence's Date is. `ReadmissionCount` compares the member's readmission count against the configured value. `Manual` fires only when the admin invokes the rule explicitly.
5. Applies the outcome. The outcome vocabulary is `Notify`, `SetStatus`, and `NoOp`. `Notify` composes and sends a notification through the configured channel. `SetStatus` sets `Member.MemberStatusID` to the status named by the rule. `NoOp` does nothing. The engine does not create, modify, or delete `Readmission` rows. A member's readmission count is derived: it is the number of `Readmission` rows recorded for that member. The count is not stored, and the rule engine does not increment it. The engine reads `Readmission` and never writes it. Process 3.0 owns Readmission creation.

**Rule evaluation order.** The engine evaluates applicable Attendance Rules sequentially, in ascending `AttendanceRuleID` order. Each rule's outcomes take effect before the next rule is evaluated. A rule is not re-evaluated after a later rule changes member state. This is sequential stateful evaluation, not merely a deterministic ordering: earlier rule outcomes are visible to later rules.

Four consequences follow from sequential evaluation:

- Two `SetStatus` rules for the same member produce the second rule's status, because its write occurs later.
- A `SetStatus` rule and a `Notify` rule for the same member both take effect. The notification is not suppressed by the status change.
- Two `Notify` rules for the same member produce two notifications. The engine does not deduplicate, merge, or arbitrate between rules.
- A later rule's trigger reads the state produced by earlier rules in the same evaluation sequence.

The engine does not arbitrate between rules. The administrator is responsible for scoping rules so their effects are what the administrator intends. Ascending `AttendanceRuleID` is the normative ordering; the specification does not provide a priority column, and reordering rules by editing primary keys is not a supported administrative mechanism.

**Determinism.** Rule evaluation is deterministic for a given set of persisted inputs: the same Attendance Rules, the same rule order by AttendanceRuleID, the same scope criteria, the same configuration, the same member population state, the same attendance facts, and the same calendar date in the same timezone produce the same outcome sequence. This is a statement about determinism, not idempotency. Running the engine twice with the same inputs may apply the same `SetStatus` outcome twice if the trigger still holds. Absence evaluation specifically is idempotent for a given calendar date: two runs on the same date in the same timezone evaluate the same absence window and produce the same absence set.

**Idempotency.** The engine is not idempotent with respect to its external outcomes. Repeating a run while a trigger remains true may produce another notification or another outcome application, even when the member's resulting state is unchanged. The engine does not maintain per-rule execution history. Whether the persistence layer optimises an identical status write away is an implementation detail and is not part of the behavioural contract. The administrator controls repetition by ensuring the trigger condition ceases to hold once the outcome has been applied — for example, by placing a `SetStatus` rule earlier in the ordering so that a later notifying rule's scope no longer matches.

**Manual invocation.** An administrator may invoke an Attendance Rule whose `TriggerType` is `Manual`. The invocation names exactly one rule. The engine resolves that rule's scope against the member population and applies the rule's outcome to every matching member. The `Manual` trigger fires unconditionally: there is no threshold and `TriggerValue` is unused. The invocation evaluates exactly the named rule and no other rule; the B8 evaluation ordering does not apply to it, because it evaluates one rule in isolation. Repeat invocation applies the outcome again, consistent with the idempotency contract above. An administrator who needs to act on one specific member edits `Member.MemberStatusID` directly through the member-management process; the `Manual` trigger is not that operation.

**Source:** System Design Specification §9.6, §13.

### 13.7 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §9 — Configuration layer.
- §10 — Generate Assignment.
- §11 — Manage Confirmation.
- §12 — Evaluate Fill Status.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model.

*End of §13. This section derives from the locked 6.0 contract and does not extend it.*

---

## 14. Dispatch Notification (9.0) — Derived Consequences

*This appendix derives, from the locked 9.0 contract, the concrete schema behavior, data flows, invariants, and indexing the process requires. Every consequence traces to a locked decision. Where an implementation choice remains genuinely open, it is named in §14.5.*

### 14.0 Process Boundary

9.0 Dispatch Notification is the outbound process that sends assignment notices to members. It is invoked by 5.0, 7.0, 12.0, and 13.0 under the conditions stated in §14.1.3 and §16.2. 9.0 is the system's only process whose primary effect is an external side effect rather than a write to an internal data store.

It reads the assignment, the member, the occurrence, and the configured channel, composes a message, sends it, and stops. It does not write to any store, does not track delivery, and does not invoke any other process. Retry semantics differ by path: the member-facing path does not retry; the authority-notification sweep retries through subsequent sweeps while `NotifiedAt` remains null. See §14.1.4 and §14.1.5.

**At a glance:**

- **Trigger:** Path 1 — invoked by 5.0, 7.0, 12.0, and 13.0 on new-assignment creation and on Notify outcomes. Path 2 — a scheduled authority-notification sweep, triggered by the scheduler.
- **Reads:** D7 RosterAssignment, D4 Member, D3 ServiceOccurrence, D11 SystemSetting.
- **Writes:** none.
- **Does not:** track delivery, retry, log, invoke any other process.
- **Fire-and-forget.**

### 14.1 Derived Schema Behavior

#### 14.1.1 Writes

9.0 has two paths, and their write footprints differ.

**Path 1 — member-facing.** 9.0 does not write to any store on this path. The notification is composed, sent, and forgotten. No delivery outcome is observable by the system. If a member does not respond to an assignment, 7.0 may eventually time it out according to its normal timeout rules, regardless of whether the absence of response resulted from notification failure.

**Path 2 — authority-facing.** 9.0 writes `ConfigurationAuditLog.NotifiedAt` on each audit row it processes. This is the only write 9.0 performs. There is no notification log, no retry table, and no delivery-status field. `NotifiedAt` records that the sweep has processed the row, not that the underlying transport delivered the message.

#### 14.1.2 Channel selection

9.0 reads SystemSetting.NotificationChannel. If the setting is absent or otherwise invalid, 9.0 follows the system setting's configured validity behavior; no channel-specific fallback is defined by this contract.

The setting names the channel — SMS, email, push, or another mechanism — and the actual credentials and transport live outside the schema.

Whether NotificationChannel.Required is true or false is a configuration choice, not fixed by the 9.0 contract.

The current scope assumes a single global channel. Per-member channel preferences are not modeled.

#### 14.1.3 Trigger boundary

9.0 has two trigger paths.

**Path 1 — member-facing, invoked.** Invoked by four processes, under the conditions below:

| Invoked by | When |
| --- | --- |
| 5.0 Generate Assignment | On each new automatic assignment created |
| 7.0 Manage Confirmation | On each replacement assignment created after decline or timeout |
| 12.0 Create Manual Assignment | Only when the created manual assignment has AssignmentStatusID = NULL at creation |
| 13.0 Attendance Rule Engine | On each Attendance Rule whose OutcomeType = Notify and whose trigger fires |

**Path 2 — authority-facing, scheduled.** A scheduled authority-notification sweep runs 9.0 on a cadence. On each run, 9.0 reads new Configuration Audit Log entries and dispatches notifications for them.

9.0 is **not** invoked on:

- Status changes to an existing assignment.
- Fill status changes by 10.0.
- Attendance records by 6.0.
- Any occurrence materialization.

**Invocation versus triggering for 9.0.** The four rows in Path 1 are invocations: 5.0, 7.0, 12.0, and 13.0 call 9.0 directly. Path 2 is a trigger, not an invocation. No process calls 9.0 for the authority-notification sweep; the scheduler starts 9.0 directly. The durable invariant is unaffected — 9.0 is still the sole process any other process is permitted to invoke.

#### 14.1.4 No delivery tracking

The system does not record that a notification was sent, when, through which channel, or whether it was delivered. There is no NotificationLog.

**Authority-notification reliability.** The authority-notification path is at-least-once with respect to the audit row's `NotifiedAt` field. A `ConfigurationAuditLog` row whose `NotifiedAt` is NULL is eligible for a future sweep. If dispatch fails, `NotifiedAt` is left NULL and a later sweep retries. If dispatch succeeds but the process fails before `NotifiedAt` is persisted, the same audit row may be dispatched again on a later sweep. This is a consequence of the design and is stated deliberately: the system does not attempt exactly-once delivery. There is no bounded retry and no attempt counter. A persistent failure can leave a row indefinitely unnotified. The absence of `NotifiedAt` is the only signal.

The member-facing path (Path 1) does not carry this semantics. Its invocation is fire-and-forget and its success is not tracked at all, per §10.1.8.

#### 14.1.5 No retry on the member-facing path

On the member-facing path (Path 1), if the underlying channel fails, the notification is lost from the system's perspective. 9.0 does not retry the member-facing dispatch.

The authority-notification path (Path 2) is retried implicitly: a `ConfigurationAuditLog` row whose `NotifiedAt` remains NULL is eligible for a subsequent sweep. See §14.1.4.

#### 14.1.6 Content

The notification carries enough information for the member to understand what they are being asked to respond to: the occurrence date and time, the duty, and (implicitly) the expectation of a response. The exact format is channel-dependent and outside the schema.

The notification does not contain a confirmation token or other schema-defined response identifier. Assignment-response correlation is handled by the 7.0 response mechanism and is outside 9.0's schema contract.

### 14.1.9 Recipient lifecycle

Recipient resolution produces zero or more admins from the matching `NotificationSubscription` rows. The following rules apply:

- An inactive or soft-deleted subscription is not consulted.
- A subscription by tier resolves to all admins at that tier whose `Admin` row is not soft-deleted. Admins whose member is soft-deleted are skipped. If the referenced `PermissionTier` is soft-deleted, the subscription resolves to zero recipients.
- A subscription by specific admin resolves to that admin if the `Admin` row is not soft-deleted. Otherwise it resolves to zero recipients.
- Zero recipients is a valid outcome, not a failure. The sweep sets `NotifiedAt` on the audit row anyway.
- No fallback recipient is invented. If the configured recipients cannot be resolved, no notification is sent and none is sought elsewhere.

### 14.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D7 RosterAssignment | The assignment being notified (Path 1) |
| R2 | D4 Member | Contact details (Path 1) |
| R3 | D3 ServiceOccurrence | Occurrence date and time (Path 1) |
| R4 | D11 SystemSetting | NotificationChannel |
| R5 | ConfigurationAuditLog | New audit rows to process (Path 2) |
| R6 | NotificationSubscription | Recipients for each audit row (Path 2) |
| R7 | Admin, PermissionTier | Resolve recipients by tier or by specific admin (Path 2) |

**Writes:** `ConfigurationAuditLog.NotifiedAt`, on Path 2 only. No other write on either path.

**Invocations:** None. 9.0 does not invoke any process.

**Stores never touched (write):** Every store except `ConfigurationAuditLog`. The `NotifiedAt` column is the only write 9.0 performs.

### 14.3 Invariants

- Outbound only. 9.0's product is a message sent to a member on Path 1, and a message sent to one or more authorities on Path 2.
- Read-only on every store except `ConfigurationAuditLog`, where `NotifiedAt` is written on Path 2.
- Trigger is bounded. Path 1 is invoked only by 5.0, 7.0, 12.0, and 13.0, on new assignment creation that requires a response or on a Notify outcome. Path 2 is a scheduled sweep, not an invocation.
- No invocation on status change.
- No invocation on any other event.
- Channel selected by setting. Follows the setting's configured validity behavior.
- Fire-and-forget on Path 1. At-least-once on Path 2, through retries while `NotifiedAt` remains null.
- No retry on Path 1.
- No delivery log on either path.
- No token.
- Channel-agnostic contract. SMS, email, push, and others are acceptable implementations behind the same contract.

### 14.4 Indexes

9.0 does not require any indexes. It reads by primary key from RosterAssignment, Member, and ServiceOccurrence, and by key from SystemSetting. All are served by existing primary-key or foreign-key indexes.

No index is prescribed at specification level for 9.0.

### 14.5 Explicitly Open Implementation Choices

- The channel mechanism.
- The message format.
- Per-member channel preferences.
- Delivery tracking.
- Retry policy.
- Confirmation token or deep-link mechanism.
- Whether 9.0's invocation is synchronous or asynchronous from the invoking process's perspective.
- Whether NotificationChannel.Required is true or false.

### 14.6 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §9 — Configuration layer.
- §10 — Generate Assignment.
- §11 — Manage Confirmation.
- §12 — Evaluate Fill Status.
- §13 — Record Attendance.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model. Note: §16 distinguishes invocation from triggering. 9.0 is invoked by 5.0, 7.0, 12.0, and 13.0, and independently carries a scheduled authority-notification trigger. The scheduled trigger reads new ConfigurationAuditLog entries. It is not an invocation.

*End of §14. This section derives from the locked 9.0 contract and does not extend it.*

---

## 15. Consolidated Schema Amendments

*This section consolidates every schema addition, constraint, and configuration setting implied by the locked contracts for §7 through §14. It is the single reference an implementer reads against the physical schema. Every item traces to a locked decision in an earlier section. Nothing here is new.*

### 15.0 Purpose

Across §7 through §14, the locked contracts implied a set of additions to the schema described in §4: columns, constraints, and configuration settings. This section gathers them in one place.

The original additions fall into four categories:

1. New columns
2. New constraints
3. New settings
4. Nullability clarifications

The original set was fifteen amendments. Two later sets have been applied since:

- The A–G amendment set (§15.8.1 through §15.8.18). Eight new entities, nine new columns, two extended criteria types, three new settings, and three removed columns.
- The attendance amendment set (§15.8.19 through §15.8.26). Three new entities, three new columns, one new process, and two behaviour rules.

Both later sets are listed in §15.8 Later Amendments.

### 15.1 New Columns

#### 15.1.1 RosterAssignment.CreatedAt

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Column | CreatedAt |
| Type | timestamp |
| Nullable | No |
| Set by | Every newly inserted row sets CreatedAt to its creation time. The creating process supplies it. |
| Purpose | Basis for 7.0's timeout calculation: now - CreatedAt >= ConfirmationTimeoutHours |
| Source | §11 |

#### 15.1.2 AssignmentStatus.IsTerminal

| Property | Value |
| --- | --- |
| Table | AssignmentStatus |
| Column | IsTerminal |
| Type | boolean |
| Nullable | No |
| Set by | Configured by the Administrator when defining the status |
| Purpose | Distinguishes statuses that occupy a required slot from statuses that leave the slot vacant |
| Source | §11 |

**Semantic note:** IsTerminal is a capacity concept, not a lifecycle-impermanence concept. A terminal assignment's row remains; the member may rejoin a slot through a new assignment.

#### 15.1.3 ServiceOccurrence columns — no amendments

The materialization contract (§7, §8) references ten columns of ServiceOccurrence: OccurrenceID, ScheduleID, EventID, Date, ServiceTypeID (override), StartTime (override), FillStatusID, GeneratedBy, CreatedBy, ChangedBy. All ten are defined in §4. The contract fixed their semantics and the values the Materializer writes; it did not require new columns. No amendment to ServiceOccurrence columns is implied by §7 through §14.

### 15.2 New Constraints

#### 15.2.1 RosterAssignment (MemberID, DutyID, OccurrenceID) unique

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Constraint | UNIQUE on (MemberID, DutyID, OccurrenceID) |
| Purpose | Prevent duplicate member/duty/occurrence records |
| Source | §10 |

#### 15.2.2 AttendanceRecord (MemberID, OccurrenceID) unique

| Property | Value |
| --- | --- |
| Table | AttendanceRecord |
| Constraint | UNIQUE on (MemberID, OccurrenceID) |
| Purpose | At most one recorded attendance fact per member per occurrence |
| Source | §13 |

#### 15.2.3 ServiceSchedule (ServiceDefID, TimeSlotID) active-row unique

| Property | Value |
| --- | --- |
| Table | ServiceSchedule |
| Constraint | UNIQUE on (ServiceDefID, TimeSlotID), restricted to active rows |
| Purpose | An active ServiceSchedule is uniquely identified by the pair |
| Source | §9 |

#### 15.2.4 ServiceOccurrence (ScheduleID, Date) schedule-sourced unique

| Property | Value |
| --- | --- |
| Table | ServiceOccurrence |
| Constraint | UNIQUE on (ScheduleID, Date), restricted to rows where ScheduleID IS NOT NULL |
| Purpose | One occurrence per schedule per date; correctness guard for 11.0 idempotency |
| Source | §7, §8 |

Event-sourced occurrences are excluded. Their uniqueness is governed by the 8.0 process.

### 15.3 New Settings

#### 15.3.1 OccurrenceHorizonDays

| Property | Value |
| --- | --- |
| Type | integer, unit days |
| Minimum | 0 |
| Required | true |
| Consumed by | 11.0 |
| Behavior if absent | 11.0 refuses to run and surfaces a configuration error |
| Source | §7, §8 |

#### 15.3.2 InitialAssignmentStatusID

| Property | Value |
| --- | --- |
| Type | integer — an AssignmentStatusID |
| Required | true |
| Consumed by | 7.0 |
| Behavior if absent | 7.0 refuses to run and surfaces a configuration error |
| Source | §11 |

#### 15.3.2a DeclinedStatusID

| Property | Value |
| --- | --- |
| Type | integer — an AssignmentStatusID |
| Required | true |
| Consumed by | 7.0, on the decline path |
| Behavior if absent | 7.0 refuses the decline operation and surfaces a configuration error |
| Source | §11 |

#### 15.3.2b TimedOutStatusID

| Property | Value |
| --- | --- |
| Type | integer — an AssignmentStatusID |
| Required | true |
| Consumed by | 7.0, on the timeout sweep path |
| Behavior if absent | 7.0 refuses the sweep operation and surfaces a configuration error |
| Source | §11 |

#### 15.3.3 ConfirmationTimeoutHours

| Property | Value |
| --- | --- |
| Type | integer, unit hours |
| Required | Configurable |
| Consumed by | 7.0 |
| Behavior if absent | If Required = false, no timeout occurs; if Required = true, 7.0 refuses to run |
| Timeout rule | now - CreatedAt >= ConfirmationTimeoutHours |
| Source | §11 |

#### 15.3.4 OutcomeStateUnfilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 |

#### 15.3.5 OutcomeStatePartiallyFilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 |

#### 15.3.6 OutcomeStateFilledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12 |

#### 15.3.7 OutcomeStateCancelledID

| Property | Value |
| --- | --- |
| Type | integer — an OutcomeStateID |
| Required | false |
| Consumed by | 10.0 |
| Behavior if absent | No automatic cancellation exclusion applies |
| Behavior if present | 10.0 skips occurrences whose FillStatusID equals that ID |
| Note | 10.0 never writes the cancelled state |
| Source | §12 |

#### 15.3.8 NotificationChannel

| Property | Value |
| --- | --- |
| Type | string or enum |
| Required | Not fixed by the specification; a deployment choice |
| Consumed by | 9.0 |
| Validation | If no valid channel is configured, 9.0 does not send. No channel-specific fallback is defined by the 9.0 contract. |
| Source | §14 |

### 15.4 Nullability Clarifications

#### 15.4.1 RosterAssignment.AssignmentStatusID — nullable

| Property | Value |
| --- | --- |
| Table | RosterAssignment |
| Column | AssignmentStatusID |
| Nullable | Yes |
| Source | §10 |

5.0 creates assignments with AssignmentStatusID = NULL; 7.0 transitions to the initial status.

### 15.5 Physical Expression Choices

- Filtered/partial unique constraints — the "restricted to active rows" and "restricted to non-null ScheduleID" conditions.
- Column types — enum vs. string vs. lookup.
- Timestamp precision.
- Composite index shapes.

### 15.6 What Is Deliberately Not Added

- No NotificationLog or delivery-tracking table.
- No AssignmentRun or equivalent log for 5.0.
- No status-history table for RosterAssignment.
- No attendance-event table.
- No retry table.
- No orchestration entity.
- No DeactivatedBy / DeactivatedAt columns on configuration entities.
- No AttendanceSource field.
- No confirmation-token or deep-link field.
- No attendance-window engine or configuration.
- No cross-service time-conflict engine.
- No MemberRole or MemberBranch join tables.
- No reserved criteria activation.

### 15.7 Summary Table

| # | Amendment | Type | Source |
| --- | --- | --- | --- |
| 15.1.1 | RosterAssignment.CreatedAt | New column | §11 |
| 15.1.2 | AssignmentStatus.IsTerminal | New column | §11 |
| 15.2.1 | RosterAssignment (MemberID, DutyID, OccurrenceID) unique | New constraint | §10 |
| 15.2.2 | AttendanceRecord (MemberID, OccurrenceID) unique | New constraint | §13 |
| 15.2.3 | ServiceSchedule (ServiceDefID, TimeSlotID) active-row unique | New constraint | §9 |
| 15.2.4 | ServiceOccurrence (ScheduleID, Date) schedule-sourced unique | New constraint | §7, §8 |
| 15.3.1 | OccurrenceHorizonDays | New setting | §7, §8 |
| 15.3.2 | InitialAssignmentStatusID | New setting | §11 |
| 15.3.3 | ConfirmationTimeoutHours | New setting | §11 |
| 15.3.4 | OutcomeStateUnfilledID | New setting | §12 |
| 15.3.5 | OutcomeStatePartiallyFilledID | New setting | §12 |
| 15.3.6 | OutcomeStateFilledID | New setting | §12 |
| 15.3.7 | OutcomeStateCancelledID | New setting | §12 |
| 15.3.8 | NotificationChannel | New setting | §14 |
| 15.4.1 | RosterAssignment.AssignmentStatusID nullable | Nullability clarification | §10 |

Fifteen amendments from the original set. Two new columns, four new constraints, eight new settings, one nullability clarification.

### 15.8 Later Amendments

The following amendments were added after the original fifteen.

| # | Amendment | Type | Source |
| --- | --- | --- | --- |
| 15.8.1 | MemberStatus entity and Member.MemberStatusID replaces Member.IsActive | New entity, new column | §4, §9.6 |
| 15.8.2 | Member.ReceiptNumber, Member.CardNumber | New columns | §4 |
| 15.8.3 | Member.JoinReason | New column | §4 |
| 15.8.4 | AttributeType entity | New entity | §4 |
| 15.8.5 | MemberAttributeValue entity | New entity | §4 |
| 15.8.6 | DutyRule.ServiceDefID | New column | §4, §10.1.5 |
| 15.8.7 | MemberAttribute and Youth criteria types | Extended vocabulary | §5, §10.1.5 |
| 15.8.8 | Event.Location removed; Event.HostBranchID added | Column change | §4 |
| 15.8.9 | EventBranch entity | New entity | §4 |
| 15.8.10 | EventDuty.Label removed; EventDuty.DutyID and EventDuty.ServiceDefID added | Column change | §4 |
| 15.8.11 | ProgramItem.Location | New column | §4 |
| 15.8.12 | IsDeleted on every configuration entity | New column, many entities | §4, §9.6 |
| 15.8.13 | Role.IsDefault | New column | §4 |
| 15.8.14 | ConfigurationAuditLog entity | New entity | §4, §9.6 |
| 15.8.15 | EntityDeletionPolicy entity | New entity | §4, §9.6 |
| 15.8.16 | NotificationSubscription entity | New entity | §4, §14.1.3 |
| 15.8.17 | Capability entity | New entity | §4 |
| 15.8.18 | ReceiptToCardDurationDays, YouthAgeMin, YouthAgeMax settings | New settings | §4 |
| 15.8.19 | AttendanceRecord.Source | New column | §4 |
| 15.8.20 | Branch.UsesAttendanceRegister, Event.UsesAttendanceRegister | New columns | §4, §9.6 |
| 15.8.21 | AttendanceRule entity | New entity | §4, §13.6 |
| 15.8.22 | AttendanceRuleScope entity | New entity | §4, §13.6 |
| 15.8.23 | Readmission entity | New entity | §4, §13.6 |
| 15.8.24 | 13.0 Attendance Rule Engine | New process | §13.6, §16 |
| 15.8.25 | Attendance register scope resolution | Rule | §9.6 |
| 15.8.26 | Branch-Attendance Recency skips when register off | Rule | §10.1.5 |

---

## 16. System-Wide Invocation Model

*This section formalizes the system's process topology: which processes invoke which other processes, and which communicate only by shared data. It states the current direct process-invocation edges in the system, and the durable invariant that underlies them.*

### 16.0 The Integration Principle

The system's default integration mechanism is **shared data**. Processes read from and write to common data stores. When one process's output needs to reach another process, it does so through a store, not through a call.

Direct process invocation is the exception. It exists only where a process has just created a new RosterAssignment row that requires a response from a member — meaning 9.0 Dispatch Notification must be invoked at that moment, rather than deferred to a polling process. No other process is ever invoked by another process.

The system distinguishes **invocation** from **triggering**. Invocation is a process-to-process call. Triggering is what starts a process running — an administrator action, a scheduled run, a member action, or an invocation by another process. A process may have more than one trigger, and it may both be invokable and carry its own independent triggers.

The durable invariant is stated as: **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** The number of processes that invoke it may change as the system grows; the invariant does not. This invariant concerns invocation, not triggering. 9.0 additionally carries an independent scheduled trigger for authority notifications, which is a trigger and not an invocation.

### 16.1 The Full Topology

    1.0 Configure vocabulary
    2.0 Configure duty rules
    3.0 Manage membership
    4.0 Manage eligibility
    8.0 Manage events/programs
            | writes
            v
    Configuration stores: D1 D2 D3 D4 D5 D6 D9 D10 D11
    (8.0 additionally creates event-sourced ServiceOccurrence rows
    when a ProgramItem is linked to a ServiceDefinition)
            | read by
            v
    11.0 Materialize Occurrences
            | writes
            v
    ServiceOccurrence (D3)
            | read by
            v
    5.0 Generate Assignment
            | writes                | invokes
            v                       v
    RosterAssignment (D7)      9.0 Dispatch Notification
            | read/written by       ^
            v                       |
    7.0 Manage Confirmation --------+
            |                       (invokes on replacement)
            | feeds
            v
    10.0 Evaluate Fill Status
            | writes
            v
    ServiceOccurrence.FillStatusID

    6.0 Record Attendance
            | writes
            v
    AttendanceRecord (D8)
    (Read by 5.0 only when a Branch-Attendance Recency criterion exists.)

### 16.2 The Direct Invocation Edges

Four direct invocation edges currently exist. All four terminate at 9.0 Dispatch Notification.

| # | From | To | Trigger | Conditionality |
| --- | --- | --- | --- | --- |
| I1 | 5.0 Generate Assignment | 9.0 Dispatch Notification | On each new automatic assignment created | Unconditional — fires on every new automatic assignment |
| I2 | 7.0 Manage Confirmation | 9.0 Dispatch Notification | On each replacement assignment created after decline or timeout | Conditional — does not fire on status changes of existing assignments |
| I3 | 12.0 Create Manual Assignment | 9.0 Dispatch Notification | Only when the created manual assignment has AssignmentStatusID = NULL at creation | Conditional — does not fire when the admin sets a non-null status directly |
| I4 | 13.0 Attendance Rule Engine | 9.0 Dispatch Notification | On each Attendance Rule whose OutcomeType = Notify and whose trigger fires | Conditional — does not fire for SetStatus or NoOp outcomes |

The **durable invariant** is not the count. It is: **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** The count may grow as the system grows; the invariant does not.

Conditionality is not a property of any specific edge. 7.0's edge (I2), 12.0's edge (I3), and 13.0's edge (I4) are all conditional by design. 5.0's edge (I1) is unconditional only because every automatic assignment it creates requires a response by definition.

Edge I4 was added by the attendance amendment set. The Attendance Rule engine invokes 9.0 only when a rule's outcome is to notify. The other two outcomes — SetStatus and NoOp — do not invoke 9.0.

### 16.3 Complete Invocation Matrix

| Process | Invokes | Invoked by |
| --- | --- | --- |
| 1.0 Configure vocabulary | — | — |
| 2.0 Configure duty rules | — | — |
| 3.0 Manage membership | — | — |
| 4.0 Manage eligibility | — | — |
| 5.0 Generate assignment | 9.0 | — |
| 6.0 Record attendance | — | — |
| 7.0 Manage confirmation | 9.0 | — |
| 8.0 Manage events and programs | — | — |
| 9.0 Dispatch notification | — | 5.0, 7.0, 12.0, 13.0 |
| 10.0 Evaluate fill status | — | — |
| 11.0 Materialize occurrences | — | — |
| 12.0 Create manual assignment | 9.0 | — |
| 13.0 Attendance Rule Engine | — | — |

### 16.4 Trigger Summary

| Process | Triggers |
| --- | --- |
| 1.0 Configure vocabulary | Administrator action |
| 2.0 Configure duty rules | Administrator action |
| 3.0 Manage membership | Administrator action |
| 4.0 Manage eligibility | Administrator action |
| 5.0 Generate assignment | Scheduled run; manual admin trigger |
| 6.0 Record attendance | Member action; manual admin entry |
| 7.0 Manage confirmation | Member response; scheduled timeout check |
| 8.0 Manage events and programs | Administrator action |
| 9.0 Dispatch notification | Invocation by 5.0, 7.0, 12.0, or 13.0; scheduled authority-notification sweep |
| 10.0 Evaluate fill status | Periodic sweep; internally observes assignment changes since the last run |
| 11.0 Materialize occurrences | Scheduled run; manual admin trigger |
| 12.0 Create manual assignment | Administrator action |
| 13.0 Attendance Rule Engine | Scheduled run; manual admin trigger |

Every process has an independent trigger mechanism except 9.0. Operational data dependencies remain: a process may require records produced by another process to exist before meaningful work can be performed. These are data dependencies, not process dependencies.

**On 10.0's trigger.** The phrase "after assignment changes" describes when 10.0's work becomes necessary, not how 10.0 learns that it has become necessary. No process invokes 10.0. It observes the current state of RosterAssignment on its own periodic sweep. The sweep is what turns assignment changes into evaluated fill status. This is the same data-mediated relationship the rest of §16 describes: the writer (5.0 or 7.0) writes to D7, the reader (10.0) reads D7 on its own trigger. No invocation edge involving 10.0 exists. The four edges that do exist are stated in §16.2.

**On the trigger matrix versus the invocation matrix.** The table above is the trigger matrix. It is distinct from the invocation matrix in §16.3, because triggering and invocation are two different things. 9.0 Dispatch Notification appears in the trigger matrix with two triggers: it is invoked by 5.0, 7.0, 12.0, and 13.0 on new-assignment creation and on Notify outcomes, and it independently runs a scheduled authority-notification sweep that reads new ConfigurationAuditLog entries. Only the first of those is an invocation. The scheduled sweep is a trigger — the scheduler starts 9.0 directly, and no other process calls it. The invocation matrix names four invoking processes: 5.0, 7.0, 12.0, and 13.0. No process invokes 9.0 for the authority-notification path.

### 16.5 Data Store Mediation

| Process | Reads | Writes |
| --- | --- | --- |
| 5.0 Generate assignment | D2, D3, D4, D6, D7, D8 (conditional) | D7 |
| 6.0 Record attendance | D3, D4 | D8 |
| 7.0 Manage confirmation | D2 (re-res), D3 (re-res), D4, D6 (re-res), D7, D8 (re-res), D10, D11 | D7 |
| 9.0 Dispatch notification | D3, D4, D7, D11 (Path 1); ConfigurationAuditLog, NotificationSubscription, Admin, PermissionTier (Path 2) | ConfigurationAuditLog (NotifiedAt, Path 2) |
| 10.0 Evaluate fill status | D3, D7, D10, D11 | D3 |
| 11.0 Materialize occurrences | D3, D11 | D3, D12 |
| 12.0 Create manual assignment | D2, D3, D4, D6, D7 | D7 |
| 13.0 Attendance Rule Engine | D2 (Attendance Rule), D3, D4, D5 (Readmission), D7 (Attendance Record), D10 | D4 (Member Status) |

### 16.6 Invariants of the Invocation Model

- **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** This is the durable form of the invariant; the current count of invoking processes is four (5.0, 7.0, 12.0, 13.0), but the invariant is stated without a count so it survives future growth. This invariant concerns invocation, not triggering.
- **Four direct invocation edges currently exist.** 5.0 → 9.0 (unconditional), 7.0 → 9.0 (conditional, on replacement only), 12.0 → 9.0 (conditional, only when AssignmentStatusID is NULL at creation), 13.0 → 9.0 (conditional, only when an Attendance Rule's outcome is Notify). All four terminate at 9.0.
- **13.0 Attendance Rule Engine is invoked by nothing.** It is triggered by the scheduler or by an administrator. It writes `Member.MemberStatusID` only; it does not write `Readmission`. Its only invocation is to 9.0, and only on Notify outcomes.
- **9.0 additionally has an independent scheduled trigger.** The authority-notification sweep is a scheduled trigger of 9.0, not an invocation. It reads new ConfigurationAuditLog entries and dispatches notifications for them. No process invokes 9.0 for this path; the scheduler starts 9.0 directly.
- **Shared data is the default integration mechanism.**
- **No orchestration.** No process orchestrates another.
- **No process depends on another process's successful completion for its own persisted business result.** 5.0 and 7.0 invoke 9.0 fire-and-forget; whether the invocation is technically synchronous or asynchronous is an implementation choice and does not create a semantic dependency.
- **Every process has an independent trigger except 9.0.**
- **Fire-and-forget notification.**
- **No feedback loops.** The invocation graph is acyclic and terminates at 9.0.

### 16.7 What This Preserves

- **Independent execution.** Each process can be scheduled or invoked independently. Their data dependencies do not require process-to-process invocation.
- **Independent testability.** Every process can be tested in isolation.
- **Independent observability.** Every process's behavior is visible through the stores it writes, except 9.0, whose contract explicitly excludes delivery tracking.

### 16.8 Cross-References

- §7 through §14 — the process contracts whose invocations are formalized here.
- §15 — the schema amendments.

*End of §16. This section formalizes the invocation topology implied by §7 through §14 and does not extend it.*

---

## 17. Create Manual Assignment (12.0) — Locked Contract and Derived Consequences

*Originates the process contract referenced but never defined elsewhere in this specification — by RosterAssignment.AssignmentSource = Manual, by AssignmentStatus.AssignedBy, and by §11.1.1's explicit statement that manual assignment "belongs to a separate admin operation." No prior lock exists for this process; this section is that lock.*

### 17.1 Locked Decisions

#### A. Trigger and scope

**A1.** Trigger is a direct administrator action — no scheduled component. This is a point-in-time operation, not a batch or recurring process.

**A2.** Applies to any Service Occurrence — schedule-sourced or event-sourced, no restriction.

**A3.** No ordering dependency on 5.0. A manual assignment can be created whether or not 5.0 has already run against the occurrence.

#### B. Rule enforcement

**B1.** Manual assignment bypasses Duty Rule tiers whose criteria type is Role, Gender, Age Range, Tenure, Membership Stage, or Branch-Attendance Recency. The admin may select any member for any duty, including one that would produce zero candidates under 5.0's automatic resolution.

**B2.** Manual assignment does **not** bypass a Duty Rule tier whose criteria type is Eligibility Flag. If any configured tier for the duty references an Eligibility Flag, the selected member must hold a current, valid Eligibility grant for that duty — regardless of assignment source. Eligibility Flag is not a ranking preference like the others; it represents a separate, accountable determination made by a specific authority, built with its own grant/revoke audit trail specifically so it could not be reduced to a bypassable preference. Letting manual assignment route around it would quietly undo that protection. Everywhere else, admin discretion is real discretion; here, it is not — because the discretion being protected belongs to whoever grants the Eligibility, not to whoever is creating the roster assignment.

**B3.** Manual assignment does not enforce the effective required slot count (RequiredSlotCount, as adjusted by Service Occurrence Duty overrides). An admin may create a manual assignment even when the duty is already at or above its configured count.

**B4.** Manual assignment does not enforce 5.0's per-occurrence availability rule. An admin may knowingly assign the same member to a second duty on the same occurrence.

**B5.** Manual assignment **does** enforce the (MemberID, DutyID, OccurrenceID) uniqueness constraint. A manual assignment duplicating an existing combination is rejected. This is a data-integrity rule, not a business rule.

#### C. Status and lifecycle

**C1.** The administrator explicitly sets AssignmentStatusID at creation, to either a specific admin-defined status or NULL.

**C2.** 9.0 Dispatch Notification is invoked only when the manually-created assignment's AssignmentStatusID is NULL at creation. A notification asking someone to confirm something the admin already recorded as confirmed would be actively confusing.

**C3.** Once created with AssignmentStatusID = NULL, a manual assignment is indistinguishable from an automatic one to every downstream process. 7.0 owns its lifecycle exactly as it would for a 5.0-created row; AssignmentSource = Manual and AssignedBy remain as permanent provenance but do not alter 7.0's or 10.0's behavior in any way.

**C4.** A manual assignment created with a non-null AssignmentStatusID is never picked up by 7.0's timeout sweep. Timeout is measured from CreatedAt for assignments awaiting a response; an assignment that was never awaiting one has nothing to time out.

#### D. Replacement interaction

**D1.** If a manual assignment later enters 7.0's lifecycle (because it started NULL) and is declined or times out, 7.0's existing re-resolution logic applies without modification, and the replacement it creates is AssignmentSource = Automatic — the same as any other 7.0-created replacement.

#### E. Authorization

**E1.** This process assumes the caller has already passed the applicable PermissionTier check. It does not itself define who is allowed to create a manual assignment.

#### F. Audit

**F1.** No separate audit or history table is introduced. The row's own fields — AssignmentSource, AssignedBy, CreatedAt — are the complete audit trail.

#### G. Duplicate submission

**G1.** A duplicate manual-assignment submission for the same (MemberID, DutyID, OccurrenceID) is rejected by the existing unique constraint — the same protection an automatic assignment already has. No additional idempotency mechanism is introduced.

#### H. Downstream effects

**H1.** Creating a manual assignment is an assignment change like any other, and is picked up by 10.0's existing "after assignment changes" trigger with no new invocation edge required.

### What this contract deliberately does not decide

Editing or removing an existing assignment (manual or automatic) is a different operation from creating one, and is out of scope here. If and when that capability is needed, it gets its own contract rather than being folded into this one.

### 17.2 Process Boundary

12.0 Create Manual Assignment is the process by which an administrator directly creates a Roster Assignment, bypassing 5.0's tiered candidate selection while still respecting Eligibility Flag criteria and the uniqueness constraint. It is the only process other than 5.0 that writes a new Roster Assignment row from scratch.

**At a glance:**

- **Trigger:** administrator action. No scheduled component.
- **Reads:** D4 Member, D3 Service Occurrence, D2 Duty Rule (Eligibility Flag tiers only), D6 Eligibility, D7 Roster Assignment (uniqueness check).
- **Writes:** D7 Roster Assignment.
- **Invokes:** 9.0 Dispatch Notification, only when the created row's AssignmentStatusID is NULL.
- **Does not:** enforce Duty Rule's non-Eligibility criteria, enforce slot capacity, enforce per-occurrence availability, create a second audit table, modify any existing row.

### 17.3 Derived Schema Behavior

#### 17.3.1 Roster Assignment — columns populated by 12.0

| Column | Value written by 12.0 |
| --- | --- |
| AssignmentID | System-generated |
| MemberID | Administrator-selected |
| DutyID | Administrator-selected |
| OccurrenceID | Administrator-selected |
| AssignmentStatusID | Administrator-selected — any admin-defined status, or NULL |
| ApprovedBy | NULL at creation |
| AssignmentSource | Manual |
| AssignedBy | The administrator performing the action |
| CreatedAt | The moment of insertion |

#### 17.3.2 Eligibility Flag enforcement

For each Duty Rule row attached to the target duty where CriteriaType = Eligibility Flag, 12.0 resolves the applicable Eligibility row using the same rule 5.0 uses (§10.1.4): the row with the latest GrantedDate whose RevokedDate is null, tiebroken by the greatest EligibilityID. If no such row exists, or the resolved row's RevokedDate is set, the manual assignment is rejected.

If the duty has no Duty Rule row with CriteriaType = Eligibility Flag, there is nothing to check, and 12.0 proceeds without consulting Eligibility at all.

#### 17.3.3 What is not checked

12.0 does not evaluate Role, Gender, Age Range, Tenure, Membership Stage, or Branch-Attendance Recency criteria. It does not compute or compare against the effective required slot count. It does not check whether the selected member already holds a non-terminal assignment elsewhere on the same occurrence.

#### 17.3.4 Interaction with 9.0

When 12.0 creates a row with AssignmentStatusID = NULL, it invokes 9.0 with that assignment, identically to 5.0's invocation. When it creates a row with a non-null AssignmentStatusID, it does not invoke 9.0.

### 17.4 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D3 Service Occurrence | The occurrence being assigned against |
| R2 | D4 Member | The selected member |
| R3 | D2 Duty Rule | Identify any Eligibility Flag criteria for the duty |
| R4 | D6 Eligibility | Resolve the applicable grant, only when R3 finds an Eligibility Flag criterion |
| R4a | D10 Assignment Status | Validate the admin-selected TargetStatusId, when one is supplied |
| R5 | D7 Roster Assignment | Uniqueness check on (MemberID, DutyID, OccurrenceID) |

**Writes:**

| # | Target | Fields |
| --- | --- | --- |
| W1 | D7 Roster Assignment | MemberID, DutyID, OccurrenceID, AssignmentStatusID (admin-selected or NULL), ApprovedBy = NULL, AssignmentSource = Manual, AssignedBy, CreatedAt |

**Invocations:**

| # | Process invoked | Trigger |
| --- | --- | --- |
| I1 | 9.0 Dispatch Notification | Only when the created row's AssignmentStatusID is NULL |

**Stores never touched:** D1, D5, D9, D11, D12. D10 is read only for Assignment Status validation when a TargetStatusId is supplied. Never modifies Service Occurrence, Service Occurrence Duty, or any existing Roster Assignment row.

### 17.5 Invariants

- Duty Rule's Role, Gender, Age Range, Tenure, Membership Stage, and Branch-Attendance Recency criteria do not gate manual assignment.
- Duty Rule's Eligibility Flag criterion does gate manual assignment, identically to how it gates automatic assignment.
- Slot capacity is not enforced.
- Per-occurrence availability is not enforced.
- (MemberID, DutyID, OccurrenceID) uniqueness is enforced, without exception.
- AssignmentStatusID is administrator-selected, not forced to NULL.
- 9.0 is invoked only when AssignmentStatusID is NULL at creation.
- Once in 7.0's lifecycle, a manually-created assignment is governed identically to an automatically-created one.
- AssignmentSource = Manual and AssignedBy are permanent provenance; they do not alter any downstream process's behavior.
- 12.0 never modifies an existing row.
- 12.0 never writes to Service Occurrence.
- No new audit table is introduced.

### 17.6 Indexes

No new index is required. The uniqueness check (R5) is served by the existing RosterAssignment (MemberID, DutyID, OccurrenceID) unique constraint from §15.2.1. The Eligibility lookup (R4) is served by the existing Eligibility (MemberID, DutyID) composite index from §10.4.

### 17.7 Explicitly Open Implementation Choices

- Whether the admin-facing interface warns when a selection would not satisfy Duty Rule's non-Eligibility criteria, versus allowing it silently. The contract permits the bypass either way; the warning is a UI choice, not a schema or process concern.
- Whether a confirmation step exists between an admin's selection and the write (e.g., a review screen). Not defined by this contract.
- Editing or removing an existing assignment — explicitly out of scope.

### 17.8 Cross-References

- §10 — Generate Assignment, whose Eligibility-resolution rule this process reuses exactly.
- §11 — Manage Confirmation, which governs a manually-created assignment identically to an automatic one once it enters the NULL-status lifecycle.
- §14 — Dispatch Notification, invoked under the same condition 5.0 and 7.0 already use.
- §15 — the existing constraints this process relies on rather than duplicating.
- §16 — the invocation model; this process adds no new invocation edge.

*End of §17. This section originates a new locked contract; it does not derive from a prior one, since none existed.*

