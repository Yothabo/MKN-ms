# Process Contracts

*Application-level command and result contracts for the thirteen processes. Derived from the System Design Specification §8–§17 and the process documents under `docs/processes/`. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states, for each process, what the process accepts as input, what identity it expects to receive, what result it returns on success, and what error categories it can produce. It is the application-level contract that sits between any caller — an HTTP endpoint, a scheduled job, another process — and the process itself.

The HTTP layer is an adapter. Its shape is not fixed by this document. The document fixes only the application contract: what the process needs, what it does, and what it returns.

---

## Conventions

**Command.** The set of values the process requires or accepts. Every command carries a field-name, a type, and whether the value is required.

**Identity.** The `Admin` record the process receives when it acts on behalf of a caller. Every process that writes configuration or operational records receives an Admin. The system does not authenticate the caller; authorisation is upstream, per the specification's stated boundary. The identity contract here is only about the shape: the process receives an `AdminID` that resolves to a non-deleted `Admin` row.

**Result.** The set of values the process returns. Every process returns a `Status` field whose value is one of `Success` or `Failure`. Failed results carry an `ErrorDetail` or a structured error.

**Errors.** The categories a process can produce. The taxonomy is consolidated in `docs/database/errors.md` (C3). This document names the categories each process can emit; C3 states what each category means.

---

## Configuration Layer

### 1.0 Configure Vocabulary

1.0 is a process group. Each subprocess has its own command.

#### 1.1 Configure Role

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| ActingAdminID | integer | Yes |

**Identity:** The Admin named by `ActingAdminID`. The process validates that the Admin exists. Authorization is upstream.

**Result on Success:** The created or updated `Role` record.

**Errors:** `ValidationFailed`, `NotFound` (the Admin does not exist).

#### 1.2 Configure Duty

Same shape as 1.1, producing a `Duty`.

#### 1.3 Configure Branch

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| Location | string | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created or updated `Branch` record.

**Errors:** `ValidationFailed`, `NotFound`.

#### 1.4 Configure Time Slot

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| BranchID | integer | Yes |
| DayOfWeek | string | Yes |
| TimeOfDayID | integer | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created or updated `BranchTimeSlot` record.

**Errors:** `ValidationFailed`, `NotFound` (Branch or TimeOfDay does not exist).

#### 1.5 Configure Service Definition

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| ServiceTypeID | integer | Yes |
| OwningBranchID | integer | No |
| ActingAdminID | integer | Yes |

**Result on Success:** The created or updated `ServiceDefinition` record.

**Errors:** `ValidationFailed`, `NotFound` (ServiceType or OwningBranch does not exist).

#### 1.6 Configure Service Duty and Schedule

Two commands.

**AddDuty command:**

| Field | Type | Required |
| --- | --- | --- |
| ServiceDefID | integer | Yes |
| DutyID | integer | Yes |
| RequiredSlotCount | integer | Yes |
| ActingAdminID | integer | Yes |

**CreateSchedule command:**

| Field | Type | Required |
| --- | --- | --- |
| ServiceDefID | integer | Yes |
| TimeSlotID | integer | Yes |
| StartTime | time | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `ServiceDefinitionDuty` or `ServiceSchedule` record.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (an active schedule already exists for the same pair).

### 2.0 Configure Duty Rules

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| DutyID | integer | Yes |
| ServiceDefID | integer | No |
| TierOrder | integer | Yes |
| CriteriaType | string | Yes |
| CriteriaValue | string | Yes |
| ActingAdminID | integer | Yes |

**Update command:** The same fields as Create, with `RuleID` added.

**Result on Success:** The created or updated `DutyRule` record.

**Errors:** `ValidationFailed` (including a `CriteriaType` outside the fixed vocabulary, or a `CriteriaValue` that does not match its grammar — the value is accepted at write time but the rule is inert if malformed; validation refuses the write if the type is invalid), `NotFound`.

### 3.0 Manage Membership

Two subprocesses.

#### 3.1 Manage Member Record

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| Surname | string | Yes |
| JoinDate | date | Yes |
| DateOfBirth | date | Yes |
| MembershipStage | string | Yes |
| Gender | string | Yes |
| Phone | string | Yes |
| Email | string | No |
| JoinReason | string | No |
| BranchID | integer | Yes |
| RoleID | integer | Yes |
| MemberStatusID | integer | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `Member` record.

**Errors:** `ValidationFailed`, `NotFound`.

#### 3.2 Manage Identifier History

**Issue command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberID | integer | Yes |
| Type | string | Yes |
| Number | string | Yes |
| AssignedDate | date | Yes |
| AuthorizedByAdminID | integer | Yes |

**Retire command:**

| Field | Type | Required |
| --- | --- | --- |
| EntryID | integer | Yes |
| UnassignedDate | date | Yes |
| Reason | string | No |
| ActingAdminID | integer | Yes |

**Result on Success:** The created or retired `IdentifierHistory` record.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (the identifier is already retired).

#### 3.x Member Management — Attendance Rule, Readmission, and Attribute operations

These three sit within 3.0's ownership boundary as established by B12 and B14.

**Create Attendance Rule command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| TriggerType | string | Yes |
| TriggerValue | string | Yes for AbsenceDays and ReadmissionCount; unused for Manual |
| OutcomeType | string | Yes |
| OutcomeStatusID | integer | Required only when OutcomeType = SetStatus |
| Enabled | boolean | Yes |
| ScopeCriteria | array of (CriteriaType, CriteriaValue) | No |
| ActingAdminID | integer | Yes |

**Record Readmission command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberID | integer | Yes |
| ReadmissionDate | date | Yes |
| Reason | string | No |
| ActingAdminID | integer | Yes |

**Set Member Attribute command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberID | integer | Yes |
| AttributeTypeID | integer | Yes |
| Value | string | Yes |
| RecordedDate | date | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `AttendanceRule`, `Readmission`, or `MemberAttributeValue` record.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (an AttendanceRule's TriggerType or OutcomeType is outside the vocabulary; a member attribute value already exists for the same pair and must be replaced).

### 4.0 Manage Eligibility

**Grant command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberID | integer | Yes |
| DutyID | integer | Yes |
| GrantedDate | date | Yes |
| GrantedByAdminID | integer | Yes |

**Revoke command:**

| Field | Type | Required |
| --- | --- | --- |
| EligibilityID | integer | Yes |
| RevokedDate | date | Yes |
| RevokedReason | string | No |
| ActingAdminID | integer | Yes |

**Result on Success:** The created or revoked `Eligibility` record.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (the grant is already revoked).

### 8.0 Manage Events and Programs

Four subprocesses.

#### 8.1 Manage Event

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| Name | string | Yes |
| StartDate | date | Yes |
| EndDate | date | Yes |
| Type | string | Yes |
| HostBranchID | integer | Yes |
| AttendingBranchIDs | array of integer | No (empty is valid per B20) |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `Event` and its `EventBranch` rows.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (the host branch or a branch in the attending set does not exist).

#### 8.2 Manage Program

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| EventID | integer | Yes |
| Title | string | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `Program`.

**Errors:** `ValidationFailed`, `NotFound`, `Conflict` (the Event already has a Program).

#### 8.3 Manage Program Item

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| ProgramID | integer | Yes |
| SequenceOrder | integer | Yes |
| Title | string | Yes |
| ScheduledStart | timestamp | Yes |
| ScheduledEnd | timestamp | Yes |
| ServiceDefID | integer | No |
| Location | string | No |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `ProgramItem`, and an event-sourced `ServiceOccurrence` when `ServiceDefID` is supplied.

**Errors:** `ValidationFailed`, `NotFound`.

#### 8.4 Manage Event Duty

**Create command:**

| Field | Type | Required |
| --- | --- | --- |
| EventID | integer | Yes |
| DutyID | integer | Yes |
| ServiceDefID | integer | No |
| RequiredSlotCount | integer | Yes |
| ActingAdminID | integer | Yes |

**Result on Success:** The created `EventDuty`.

**Errors:** `ValidationFailed`, `NotFound`.

---

## Operations Layer

### 5.0 Generate Assignment

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| OccurrenceId | integer | Supply exactly one of OccurrenceId or the date range |
| FromDate | date | As above |
| ToDate | date | As above |

**Identity:** None. 5.0 is triggered by the scheduler or by an administrator; the input carries no `ActingAdminID`. When a manual run is invoked by an administrator, that fact is not recorded on the assignment rows 5.0 creates.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| OccurrencesProcessed | integer |
| AssignmentsCreated | integer |
| DutiesFullyFilled | integer |
| DutiesPartiallyFilled | integer |
| DutiesUnfilled | integer |

**Errors:** `ValidationFailed` (neither or both shapes supplied), `ConfigurationMissing` (a required setting referenced by an active rule is absent — 5.0 does not itself require a setting, but its criteria evaluation may), `ConcurrencyConflict` (the capacity invariant could not be preserved).

### 6.0 Record Attendance

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberId | integer | Yes |
| OccurrenceId | integer | Yes |

**Identity:** None. The process records a fact. The `Source` column on the created row is `Manual` for the currently implemented path.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| Recorded | boolean |
| RecordId | integer |

**Errors:** `ValidationFailed`, `NotFound` (member or occurrence does not exist), `ConfigurationMissing` (the attendance register is off at the effective scope; the process refuses to write a row).

### 7.0 Manage Confirmation

Two commands.

**Respond command:**

| Field | Type | Required |
| --- | --- | --- |
| Operation | "Confirm" or "Decline" | Yes |
| AssignmentId | integer | Yes |

**Sweep command:** no fields.

**Identity:** None. The responding member or the scheduler. The target status is resolved from `SystemSetting`, not carried in the command.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| AssignmentsTransitioned | integer |
| ReplacementsCreated | integer |
| SlotsLeftVacant | integer |

**Errors:** `ValidationFailed`, `NotFound` (the assignment does not exist), `ConfigurationMissing` (`InitialAssignmentStatusID`, `DeclinedStatusID`, or `TimedOutStatusID` is absent), `LifecycleViolation` (the terminal-to-non-terminal guard from B3 rejected the transition).

### 9.0 Dispatch Notification

9.0 has two invocation paths and no externally callable command.

**Path 1 (invoked):** invoked by 5.0, 7.0, 12.0, or 13.0 with a `RosterAssignment` as input. No external command.

**Path 2 (scheduled sweep):** no command. Triggered by the scheduler. Reads `ConfigurationAuditLog` rows where `NotifiedAt` is null.

**Result on Success:** None. Fire-and-forget. The caller does not receive a result.

**Errors:** Never surfaces to the caller. On Path 1, the process catches its own failure and returns; the caller's assignment is committed regardless, per B7. On Path 2, a failure leaves `NotifiedAt` null and the row is retried on a later sweep, per B15.

### 10.0 Evaluate Fill Status

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| OccurrenceId | integer | Supply exactly one of OccurrenceId or the date range |
| FromDate | date | As above |
| ToDate | date | As above |

**Identity:** None.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| OccurrencesEvaluated | integer |
| UnfilledCount | integer |
| PartiallyFilledCount | integer |
| FilledCount | integer |
| SkippedCancelledCount | integer |

**Errors:** `ValidationFailed` (neither or both shapes supplied), `ConfigurationMissing` (any of `OutcomeStateUnfilledID`, `OutcomeStatePartiallyFilledID`, `OutcomeStateFilledID` is absent; 10.0 refuses the whole run, not just the affected occurrences).

### 11.0 Materialize Occurrences

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| TriggerType | "Scheduled" or "Manual" | Yes |
| TriggeredByAdminId | integer | Required when TriggerType is "Manual"; forbidden otherwise |
| HorizonDaysOverride | integer | No |

**Identity:** The Admin named by `TriggeredByAdminId` when `TriggerType` is "Manual". The process validates the Admin exists. Authorization is upstream.

**Result on Success:**

| Field | Type |
| --- | --- |
| RunId | integer |
| Status | "Success" or "Failure" |
| SchedulesEvaluated | integer |
| OccurrencesCreated | integer |

**Errors:** `ValidationFailed` (the trigger-type and admin-id combination is invalid), `ConfigurationMissing` (`OccurrenceHorizonDays` is absent and no override was supplied), `ConcurrencyConflict` (the advisory lock could not be acquired because another run is in progress).

### 12.0 Create Manual Assignment

**Command:**

| Field | Type | Required |
| --- | --- | --- |
| MemberId | integer | Yes |
| DutyId | integer | Yes |
| OccurrenceId | integer | Yes |
| TargetStatusId | integer | No |
| ActingAdminId | integer | Yes |

**Identity:** The Admin named by `ActingAdminId`.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| AssignmentId | integer |
| NotificationDispatched | boolean |

**Errors:** `ValidationFailed`, `NotFound` (member, duty, occurrence, admin, or target status does not exist), `Conflict` (the (MemberID, DutyID, OccurrenceID) uniqueness constraint is already satisfied), `ValidationFailed` (an Eligibility Flag rule for the duty is not satisfied by the selected member).

### 13.0 Attendance Rule Engine

Two commands.

**Scheduled run command:** no fields.

**Manual invocation command:**

| Field | Type | Required |
| --- | --- | --- |
| RuleId | integer | Yes |

**Identity:** None on the scheduled run. On the manual invocation, the ActingAdmin is not required by the entity shape; the invocation itself is the identity. If a future revision requires an admin identity for the manual invocation, it is an amendment.

**Result on Success:**

| Field | Type |
| --- | --- |
| Status | "Success" or "Failure" |
| RulesEvaluated | integer |
| MembersEvaluated | integer |
| StatusChangesApplied | integer |
| NotificationsDispatched | integer |

**Errors:** `ValidationFailed` (the named rule does not exist, is not enabled, or is not a Manual rule), `ConfigurationMissing` (a required setting referenced by an active rule is absent).

---

## Result envelopes

Every process returns a result carrying at least a `Status` field. The value is either `"Success"` or `"Failure"`.

**Success envelopes** carry process-specific fields as listed above. Fields describing counts carry integer zero when no operation of that kind occurred. Fields describing the created or updated entity carry the entity's primary key.

**Failure envelopes** carry:

| Field | Type | Notes |
| --- | --- | --- |
| Status | string | "Failure" |
| ErrorCode | string | One of the error categories named by the process |
| ErrorDetail | string | Human-readable description |

**Exception shape.** A failure that indicates a caller error — validation, not-found, conflict, lifecycle violation, missing configuration — is returned as a Failure result, not thrown. A failure that indicates a system fault — a database connection loss, an invariant violation that should be impossible, a serialization failure — is thrown. The caller's error handler decides what to do with a thrown exception; the specification does not require catching them at every boundary.

**No process returns a partial-success status.** A process either completes its work for the requested scope or returns Failure for the whole requested scope. A sweep that processes some rows and fails on others is not a partial success; it is a Failure, and the rows already processed remain processed (per B17's transaction boundaries, each row's work is its own atomic unit).
