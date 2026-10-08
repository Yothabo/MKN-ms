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

The OwningBranchID on a ServiceDefinition is enforced at the moment a ServiceSchedule is created or edited, not at ServiceDefinition creation.

- If OwningBranchID is null, any BranchTimeSlot may be used.
- If OwningBranchID is set, only BranchTimeSlots whose BranchID matches may be used.
- A later edit to OwningBranchID does not rewrite existing schedules.

#### 9.1.7 Member and Identifier History

Member carries its register fields plus a single BranchID and a single RoleID. Required at creation: JoinDate, DateOfBirth, MembershipStage, Name, Surname, Gender, Phone, BranchID, RoleID. Only Email is optional.

MembershipStage is a free-text value, not a lookup.

IdentifierHistory is a per-member log of identifier assignments. Type is free-text. Number is admin-supplied. AssignedDate and Number are required; UnassignedDate and Reason are optional; AuthorizedBy is a required FK to Admin. No active flag. To retire an identifier, UnassignedDate and optionally Reason are set. Entries are never deleted.

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

#### 9.1.12 D10 — Config Lookups

D10 contains admin-defined lookups referenced elsewhere in the schema: Outcome State, Assignment Status, Permission Tier, TimeOfDay, ServiceType.

#### 9.1.13 System Setting — configuration values consumed by other processes

The configuration layer writes SystemSetting rows. Some are consumed only by operational processes:

- OccurrenceHorizonDays — required; consumed by 11.0.
- InitialAssignmentStatusID — required; consumed by 7.0.
- OutcomeStateUnfilledID, OutcomeStatePartiallyFilledID, OutcomeStateFilledID — required; consumed by 10.0.
- OutcomeStateCancelledID — non-required; consumed by 10.0.
- NotificationChannel — deployment choice; consumed by 9.0.
- TenureThresholdDays, ReceiptToCardDurationDays, ConfirmationTimeoutHours, age-range bounds — consumed by whichever processes reference them.

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

### 9.6 Cross-References

- §7 — Materialization contract.
- §8 — Occurrence Materialization derivation.
- §10 — Generate Assignment.
- §11 — Manage Confirmation.
- §12 — Evaluate Fill Status.
- §13 — Record Attendance.
- §14 — Dispatch Notification.
- §15 — Consolidated schema amendments.
- §16 — System-wide invocation model.

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
| Branch-Attendance Recency | Member has an AttendanceRecord at the specified branch within the configured window |
| Eligibility Flag | Member has a current Eligibility grant for the referenced duty |

Reserved criteria types (Acceptance Rate, Duties Carried, Days Since Last Assignment) are not evaluated until a future specification revision activates them. If a tier consists entirely of rules whose criteria types are reserved, the tier is treated as producing no candidates.

#### 10.1.6 Selection within a tier

When a tier produces more candidates than slots, 5.0 fills from the candidate set in a deterministic order. No fairness guarantee is made; no rotation, load-balancing, or recency-based ordering is applied.

#### 10.1.7 Re-entry and uniqueness

RosterAssignment is unique on (MemberID, DutyID, OccurrenceID). This is the database-level guard against duplicate member/duty/occurrence records, regardless of source.

5.0 is fully re-entrant. An interrupted run resumes on the next run.

#### 10.1.8 Interaction with 9.0

When 5.0 creates a new automatic assignment, it invokes 9.0 with that assignment as input. 9.0 sends an assignment notice to the member. 5.0 does not wait for the notification result; the invocation is fire-and-forget, and 9.0's failure does not roll back the assignment.

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

- The deterministic candidate ordering within a tier.
- Eligibility resolution query shape.
- Whether 5.0's scheduled and manual runs share a code path.
- Batching strategy for filling slots across duties within an occurrence.
- Failure handling when 9.0 invocation fails.

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

The system does not enforce a state machine. Any transition between statuses is permitted at the schema level. The lifecycle semantics — which transitions are meaningful — are the admin's concern. The system records the current AssignmentStatusID and nothing else.

#### 11.1.4 Status lifecycle ownership

5.0 creates automatic assignments with AssignmentStatusID = NULL. 7.0 owns the status transitions for assignments after creation. Other processes — for example, a manual assignment operation — may also create assignments with AssignmentStatusID = NULL, subject to their own contracts.

#### 11.1.5 Initial status designation

7.0 transitions an assignment from NULL to the admin-designated initial status. This status is identified via SystemSetting.InitialAssignmentStatusID, a required setting. If the setting is unset or invalid, 7.0 refuses to run and surfaces a configuration error.

#### 11.1.6 Timeout measurement

An assignment becomes eligible for timeout when its elapsed age reaches the configured ConfirmationTimeoutHours:
now - CreatedAt >= ConfirmationTimeoutHours

The timeout check runs as a scheduled part of 7.0.

If ConfirmationTimeoutHours is unset and Required = false, no timeout occurs. If Required = true and unset, 7.0 refuses to run and surfaces a configuration error.

**Past-occurrence guard.** The timeout sweep applies a fixed system rule to guard against acting on a service that has already happened. The timeout transition is recorded for any eligible assignment, regardless of the occurrence's date. However, if the occurrence's date is before today in the configured `ApplicationTimeZone`, the 7.0 process **will not seek a replacement assignment and will not send a notification.**

This is a fixed rule, resolved via `ApplicationTimeZone`. The administrator configures `ConfirmationTimeoutHours`; the specification owns the past-occurrence exclusion. This rule adds no new setting.

#### 11.1.7 Re-resolution on decline or timeout

When a member declines or an assignment times out:

1. The assignment's AssignmentStatusID transitions to the admin-designated declined or timed-out status.
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
- Status transitions are unrestricted at the schema level.
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
- **Slot capacity is preserved under concurrency.** The specification defines the required property: a duty's non-terminal assignment count must never exceed its effective required slot count under any interleaving of concurrent writers. The mechanism that guarantees this property — row-level locking, serializable isolation, or an application-level mutex — is an implementation choice, as listed in §11.5. The process must select and apply a mechanism that satisfies the property.

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

### 13.6 Cross-References

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

9.0 Dispatch Notification is the outbound process that sends assignment notices to members. It is invoked by 5.0 and 7.0 when they create new automatic assignments. 9.0 is the system's only process whose primary effect is an external side effect rather than a write to an internal data store.

It reads the assignment, the member, the occurrence, and the configured channel, composes a message, sends it, and stops. It does not write to any store, does not track delivery, does not retry, and does not invoke any other process.

**At a glance:**

- **Trigger:** 5.0 creates an automatic assignment; 7.0 creates a replacement assignment.
- **Reads:** D7 RosterAssignment, D4 Member, D3 ServiceOccurrence, D11 SystemSetting.
- **Writes:** none.
- **Does not:** track delivery, retry, log, invoke any other process.
- **Fire-and-forget.**

### 14.1 Derived Schema Behavior

#### 14.1.1 No writes

9.0 does not write to any store. It has no persistence side effect. The notification is composed, sent, and forgotten — from the system's perspective.

This is a deliberate design choice: introducing a notification log, retry table, or delivery-status field would be new infrastructure. No delivery outcome is observable by the system. If a member does not respond to an assignment, 7.0 may eventually time it out according to its normal timeout rules, regardless of whether the absence of response resulted from notification failure.

#### 14.1.2 Channel selection

9.0 reads SystemSetting.NotificationChannel. If the setting is absent or otherwise invalid, 9.0 follows the system setting's configured validity behavior; no channel-specific fallback is defined by this contract.

The setting names the channel — SMS, email, push, or another mechanism — and the actual credentials and transport live outside the schema.

Whether NotificationChannel.Required is true or false is a configuration choice, not fixed by the 9.0 contract.

The current scope assumes a single global channel. Per-member channel preferences are not modeled.

#### 14.1.3 Trigger boundary

9.0 is invoked by three processes, under the conditions described below:

| Invoked by | When |
| --- | --- |
| 5.0 Generate Assignment | On each new automatic assignment created |
| 7.0 Manage Confirmation | On each replacement assignment created after decline or timeout |
| 12.0 Create Manual Assignment | Only when the created manual assignment has AssignmentStatusID = NULL at creation |

9.0 is **not** invoked on:

- Status changes to an existing assignment.
- Fill status changes by 10.0.
- Attendance records by 6.0.
- Any configuration change.
- Any occurrence materialization.

#### 14.1.4 No delivery tracking

The system does not record that a notification was sent, when, through which channel, or whether it was delivered. There is no NotificationLog.

#### 14.1.5 No retry

If the underlying channel fails, the notification is lost from the system's perspective. 9.0 does not retry.

#### 14.1.6 Content

The notification carries enough information for the member to understand what they are being asked to respond to: the occurrence date and time, the duty, and (implicitly) the expectation of a response. The exact format is channel-dependent and outside the schema.

The notification does not contain a confirmation token or other schema-defined response identifier. Assignment-response correlation is handled by the 7.0 response mechanism and is outside 9.0's schema contract.

### 14.2 Read and Write Footprint

**Reads:**

| # | Source | Purpose |
| --- | --- | --- |
| R1 | D7 RosterAssignment | The assignment being notified |
| R2 | D4 Member | Contact details |
| R3 | D3 ServiceOccurrence | Occurrence date and time |
| R4 | D11 SystemSetting | NotificationChannel |

**Writes:** None. 9.0 does not write to any store.

**Invocations:** None. 9.0 does not invoke any process.

**Stores never touched (write):** All stores. 9.0 is read-only on every store it touches.

### 14.3 Invariants

- Outbound only. 9.0's only product is a message sent to a member.
- Read-only on all stores.
- Trigger is bounded. Invoked only by 5.0, 7.0, and 12.0, only on new assignment creation that requires a response.
- No invocation on status change.
- No invocation on any other event.
- Channel selected by setting. Follows the setting's configured validity behavior.
- Fire-and-forget.
- No retry.
- No delivery log.
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
- §16 — System-wide invocation model.

*End of §14. This section derives from the locked 9.0 contract and does not extend it.*

---

## 15. Consolidated Schema Amendments

*This section consolidates every schema addition, constraint, and configuration setting implied by the locked contracts for §7 through §14. It is the single reference an implementer reads against the physical schema. Every item traces to a locked decision in an earlier section. Nothing here is new.*

### 15.0 Purpose

Across §7 through §14, the locked contracts implied a set of additions to the schema described in §4: columns, constraints, and configuration settings. This section gathers them in one place.

The additions fall into four categories:

1. New columns
2. New constraints
3. New settings
4. Nullability clarifications

No new entities are introduced. No new tables. Fifteen amendments total.

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

Fifteen amendments. Two new columns, four new constraints, eight new settings, one nullability clarification.

---

## 16. System-Wide Invocation Model

*This section formalizes the system's process topology: which processes invoke which other processes, and which communicate only by shared data. It states the current direct process-invocation edges in the system, and the durable invariant that underlies them.*

### 16.0 The Integration Principle

The system's default integration mechanism is **shared data**. Processes read from and write to common data stores. When one process's output needs to reach another process, it does so through a store, not through a call.

Direct process invocation is the exception. It exists only where a process has just created a new RosterAssignment row that requires a response from a member — meaning 9.0 Dispatch Notification must be invoked at that moment, rather than deferred to a polling process. No other process is ever invoked by another process.

The durable invariant is stated as: **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** The number of processes that invoke it may change as the system grows; the invariant does not.

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

Three direct invocation edges currently exist. All three terminate at 9.0 Dispatch Notification.

| # | From | To | Trigger | Conditionality |
| --- | --- | --- | --- | --- |
| I1 | 5.0 Generate Assignment | 9.0 Dispatch Notification | On each new automatic assignment created | Unconditional — fires on every new automatic assignment |
| I2 | 7.0 Manage Confirmation | 9.0 Dispatch Notification | On each replacement assignment created after decline or timeout | Conditional — does not fire on status changes of existing assignments |
| I3 | 12.0 Create Manual Assignment | 9.0 Dispatch Notification | Only when the created manual assignment has AssignmentStatusID = NULL at creation | Conditional — does not fire when the admin sets a non-null status directly |

The **durable invariant** is not the count. It is: **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** The count may grow as the system grows; the invariant does not.

Conditionality is not a property of any specific edge. 7.0's edge (I2) and 12.0's edge (I3) are both conditional by design — each fires only when a response is genuinely being requested from a member. 5.0's edge (I1) is unconditional only because every automatic assignment it creates requires a response by definition.

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
| 9.0 Dispatch notification | — | 5.0, 7.0, 12.0 |
| 10.0 Evaluate fill status | — | — |
| 11.0 Materialize occurrences | — | — |
| 12.0 Create manual assignment | 9.0 | — |

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
| 9.0 Dispatch notification | Invocation by 5.0, 7.0, or 12.0 |
| 10.0 Evaluate fill status | Periodic sweep; internally observes assignment changes since the last run |
| 11.0 Materialize occurrences | Scheduled run; manual admin trigger |
| 12.0 Create manual assignment | Administrator action |

Every process has an independent trigger mechanism except 9.0. Operational data dependencies remain: a process may require records produced by another process to exist before meaningful work can be performed. These are data dependencies, not process dependencies.

**On 10.0's trigger.** The phrase "after assignment changes" describes when 10.0's work becomes necessary, not how 10.0 learns that it has become necessary. No process invokes 10.0. It observes the current state of RosterAssignment on its own periodic sweep. The sweep is what turns assignment changes into evaluated fill status. This is the same data-mediated relationship the rest of §16 describes: the writer (5.0 or 7.0) writes to D7, the reader (10.0) reads D7 on its own trigger. No fourth invocation edge exists.

### 16.5 Data Store Mediation

| Process | Reads | Writes |
| --- | --- | --- |
| 5.0 Generate assignment | D2, D3, D4, D6, D7, D8 (conditional) | D7 |
| 6.0 Record attendance | D3, D4 | D8 |
| 7.0 Manage confirmation | D2 (re-res), D3 (re-res), D4, D6 (re-res), D7, D8 (re-res), D10, D11 | D7 |
| 9.0 Dispatch notification | D3, D4, D7, D11 | — |
| 10.0 Evaluate fill status | D3, D7, D10, D11 | D3 |
| 11.0 Materialize occurrences | D3, D11 | D3, D12 |
| 12.0 Create manual assignment | D2, D3, D4, D6, D7 | D7 |

### 16.6 Invariants of the Invocation Model

- **9.0 Dispatch Notification is the sole process any other process is permitted to invoke.** This is the durable form of the invariant; the current count of invoking processes is three (5.0, 7.0, 12.0), but the invariant is stated without a count so it survives future growth.
- **Three direct invocation edges currently exist.** 5.0 → 9.0 (unconditional), 7.0 → 9.0 (conditional, on replacement only), 12.0 → 9.0 (conditional, only when AssignmentStatusID is NULL at creation). All three terminate at 9.0.
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

