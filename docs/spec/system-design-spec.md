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

### Branch

| Column | Notes |
| --- | --- |
| BranchID | Primary key |
| Name |  |
| Location | Single entry, structured internally for lookups |

### Branch Time Slot

| Column | Notes |
| --- | --- |
| TimeSlotID | Primary key |
| BranchID | Foreign key |
| DayOfWeek |  |
| TimeOfDayID | Foreign key, references an open, administrator-defined list |

### Service Definition

| Column | Notes |
| --- | --- |
| ServiceDefID | Primary key |
| Name | Administrator-assigned label |
| ServiceTypeID | Foreign key, references an open, administrator-defined list |
| OwningBranchID | Optional — null means available to any branch; set means exclusive to one |

### Service Definition Duty

| Column | Notes |
| --- | --- |
| ServiceDefID | Foreign key |
| DutyID | Foreign key |
| RequiredSlotCount | Administrator-set integer, no default |

### Service Schedule

| Column | Notes |
| --- | --- |
| ScheduleID | Primary key |
| ServiceDefID | Foreign key |
| TimeSlotID | Foreign key |
| StartTime | Concrete clock time |
| IsActive | Boolean. True from creation. An administrator can deactivate a schedule to stop future occurrence generation without deleting it or altering any occurrence already materialized from it. |

### Service Occurrence

| Column | Notes |
| --- | --- |
| OccurrenceID | Primary key |
| ScheduleID | Optional — present for schedule-sourced occurrences |
| EventID | Optional — present for event-sourced occurrences, created directly when an administrator links a Program Item to a Service Definition, entirely outside the Occurrence Materializer |
| Date | Date-only; start time is represented separately |
| ServiceTypeID (override) | Optional |
| StartTime (override) | Optional |
| FillStatusID | Foreign key → Outcome State. Null at creation — populated only once Fill Status evaluation runs |
| GeneratedBy | System or Administrator — permanent provenance, set once at creation and never altered afterward |
| CreatedBy | Foreign key → Admin, populated only when GeneratedBy is Administrator |
| ChangedBy | Foreign key → Admin, optional — records subsequent human modification after creation, distinct from CreatedBy |

A schedule-sourced row is unique on (ScheduleID, Date), enforced at the database level with an application-level existence check as a pre-write optimization. This constraint does not apply to event-sourced rows, whose identity is governed by the Event/Program process, not by this one.

### Service Occurrence Duty

| Column | Notes |
| --- | --- |
| OccurrenceID | Foreign key |
| DutyID | Foreign key |
| Action | Added or Removed, relative to the Service Definition's normal duty list |
| RequiredSlotCount (override) | Optional — overrides the Definition's slot count for this date only |

Only occurrences with an actual override need a row here. The Occurrence Materializer never creates rows in this table.

### Materializer Run (log)

| Column | Notes |
| --- | --- |
| RunID | Primary key |
| TriggerType | Scheduled or Manual |
| TriggeredBy | Foreign key → Admin, populated only when TriggerType is Manual |
| StartedAt |  |
| CompletedAt |  |
| SchedulesEvaluated | Count |
| OccurrencesCreated | Count |
| Status | Success or Failure |
| ErrorDetail | Optional |

### Outcome State (lookup)

| Column | Notes |
| --- | --- |
| OutcomeStateID | Primary key |
| Name | Administrator-defined, open list — referenced by Service Occurrence's FillStatus. Cancellation is represented as one such state, never as deletion of the occurrence row. |

### Assignment Status (lookup)

| Column | Notes |
| --- | --- |
| AssignmentStatusID | Primary key |
| Name | Administrator-defined, open list — referenced by Roster Assignment's Status |
| IsTerminal | Boolean — distinguishes statuses that occupy a required slot from statuses that leave the slot vacant |

### System Setting

| Column | Notes |
| --- | --- |
| Key | Primary key — administrator-facing setting name |
| Value | The configured value |
| Required | Boolean. A required setting with no Value causes any process depending on it to refuse to run and surface a configuration error, rather than proceeding with an assumed value or idling silently. |
| Description | Optional, for administrator clarity |

A generic store for scalar, non-list configuration values. Currently expected to hold: `OccurrenceHorizonDays`, `InitialAssignmentStatusID`, `ConfirmationTimeoutHours`, `OutcomeStateUnfilledID`, `OutcomeStatePartiallyFilledID`, `OutcomeStateFilledID`, `OutcomeStateCancelledID`, `NotificationChannel`, tenure threshold before general duty eligibility, receipt-to-card issuance duration, and age-range bounds used by Duty Rule criteria.

### Role

| Column | Notes |
| --- | --- |
| RoleID | Primary key |
| Name | Administrator-defined, open list |
| IsActive | Boolean |

### Duty

| Column | Notes |
| --- | --- |
| DutyID | Primary key |
| Name | Administrator-defined, open list |
| IsActive | Boolean |

Carries no reference to Role, no restriction, no priority information of any kind. A Duty is a name until Duty Rules exist for it.

### Duty Rule

| Column | Notes |
| --- | --- |
| RuleID | Primary key |
| DutyID | Foreign key |
| TierOrder | Administrator-defined sequence position |
| CriteriaType | One of the system's supported criteria types |
| CriteriaValue | The specific value for that criteria, administrator-entered |
| IsActive | Boolean |

The only mechanism connecting a Role — or Gender, Age, Tenure, or any other criteria type — to a Duty. Multiple rows may share the same TierOrder for a given Duty; rows sharing a tier are ANDed.

### Member

| Column | Notes |
| --- | --- |
| MemberID | Primary key |
| JoinDate |  |
| DateOfBirth |  |
| MembershipStage | Administrator-defined |
| Name |  |
| Surname |  |
| Gender |  |
| Phone |  |
| Email | Optional |
| BranchID | Foreign key |
| RoleID | Foreign key |
| IsActive | Boolean |

### Identifier History

| Column | Notes |
| --- | --- |
| EntryID | Primary key |
| MemberID | Foreign key |
| Type | Administrator-defined identifier category |
| Number |  |
| AssignedDate |  |
| UnassignedDate | Optional |
| Reason | Optional |
| AuthorizedBy | Foreign key → Admin |

### Eligibility

| Column | Notes |
| --- | --- |
| EligibilityID | Primary key |
| MemberID | Foreign key |
| DutyID | Foreign key |
| GrantedDate |  |
| GrantedBy | Foreign key → Admin |
| RevokedDate | Optional |
| RevokedReason | Optional |

A binary, administrator-granted permission record for a member against a duty, independent of Duty Rule's tier/priority logic.

### Roster Assignment

| Column | Notes |
| --- | --- |
| AssignmentID | Primary key |
| MemberID | Foreign key |
| DutyID | Foreign key |
| OccurrenceID | Foreign key |
| AssignmentStatusID | Foreign key → Assignment Status. Nullable — null at automatic creation |
| ApprovedBy | Foreign key → Admin |
| AssignmentSource | Automatic or Manual |
| AssignedBy | Foreign key → Admin, populated only when Manual |
| CreatedAt | Timestamp — basis for confirmation timeout |

Unique on (MemberID, DutyID, OccurrenceID).

### Permission Tier (lookup)

| Column | Notes |
| --- | --- |
| PermissionTierID | Primary key |
| Name | Administrator-defined |

### Admin

| Column | Notes |
| --- | --- |
| AdminID | Primary key |
| MemberID | Foreign key |
| PermissionTierID | Foreign key → Permission Tier |

### Attendance Record

| Column | Notes |
| --- | --- |
| RecordID | Primary key |
| MemberID | Foreign key |
| OccurrenceID | Foreign key |
| Timestamp |  |

Unique on (MemberID, OccurrenceID).

### Event / Program / Program Item / Event Duty

| Entity | Columns |
| --- | --- |
| Event | EventID, Name, StartDate, EndDate, Location, Type, IsActive |
| Program | ProgramID, EventID, Title |
| Program Item | ItemID, ProgramID, SequenceOrder, Title, ScheduledStart, ScheduledEnd, ServiceDefID (optional) |
| Event Duty | EventDutyID, ProgramItemID, Label (free text), AssignedMemberID, AssignmentStatusID (optional) |

Program Item has no stored status field — its live state (upcoming/current/done) is computed at read time from scheduled times. When a Program Item's ServiceDefID is set, the system creates the corresponding Service Occurrence directly at that point, with EventID populated, ScheduleID null, GeneratedBy = Administrator, and CreatedBy = whichever admin created the Program Item — entirely outside the Occurrence Materializer's process.

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
- (Reserved for future use once historical data exists) Acceptance Rate, Duties Carried, Days Since Last Assignment

These are supported criterion *types*, not rules. Their presence in this vocabulary does not activate them anywhere.

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

Occurrences created when an administrator links a Program Item directly to a Service Definition are entirely outside this process. They carry GeneratedBy = Administrator and CreatedBy = the admin who created them, and their uniqueness is governed by the Event/Program process.

### The resulting invariant

The Occurrence Materializer is additive and idempotent. It creates missing schedule-sourced occurrences within the configured horizon and never modifies or deletes an occurrence that already exists.
