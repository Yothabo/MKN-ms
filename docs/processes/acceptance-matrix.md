# Acceptance Matrix

*One row per testable invariant. Derived from the System Design Specification and the process contracts. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states the invariants of the system as testable conditions. Each row is a Given / When / Then statement derived from an invariant already stated in the specification or in a Class B decision. The matrix adds no new rules. It renders the existing rules executable.

## Conventions

Each row carries the process, the given condition, the action, and the expected result. Where a row tests a boundary, the boundary is stated. Where a row tests a negative condition — something must not happen — the assertion is stated as a negative.

The matrix does not prescribe the mechanism. Concurrency rows state the property to test, not the locking or isolation mechanism. Idempotency rows state the repeated-outcome behaviour, not how the implementation achieves it.

## Configuration layer

### 1.0 Configure Vocabulary

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 1.0 | A new Role with a unique name | The Role is created | The Role row exists with `IsActive = true`, `IsDeleted = false` |
| 1.0 | An existing active Role | The Role is deactivated | `IsActive = false`, `IsDeleted = false`; a ConfigurationAuditLog row records the deactivation with reason and approver |
| 1.0 | An existing deactivated Role | The Role is reactivated | `IsActive = true`, `IsDeleted = false` |
| 1.0 | An existing Role referenced by a Member | A soft delete is requested | The operation is blocked if the Role is the default Role; otherwise, member RoleIDs fall back to the default Role and Duty Rules naming the Role are deleted; a ConfigurationAuditLog row records the soft delete with reason and approver |
| 1.0 | An active ServiceSchedule and a second active schedule for the same (ServiceDefID, TimeSlotID) | The second schedule is created | The operation is rejected with `Conflict`; the uniqueness constraint applies only to active, not-deleted rows |
| 1.0 | A ServiceSchedule with `IsActive = false` | A new schedule is created for the same (ServiceDefID, TimeSlotID) | The new schedule is created |
| 1.0 | A ServiceDefinition with a non-null OwningBranchID | A ServiceSchedule is created whose TimeSlot.BranchID differs | The operation is rejected with `ValidationFailed`, per B19 |
| 1.0 | A ServiceDefinition whose OwningBranchID is changed after a schedule exists | The OwningBranchID is updated | The existing schedule remains; a re-validation of the invariant is implementation-dependent; the resulting row is treated as an integrity violation, per B19 |
| 1.0 | An active ServiceSchedule whose definition is deactivated | The materializer runs | The schedule is not picked up; existing occurrences remain unchanged |

### 2.0 Configure Duty Rules

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 2.0 | A Duty with two tiers, tier 1 having two AND-ed rules | A candidate matches only one of tier 1's rules | The candidate is not selected for tier 1; the engine falls through to tier 2 |
| 2.0 | A Duty Rule with `ServiceDefID = null` | A service requiring the Duty is processed | The rule applies |
| 2.0 | A Duty Rule with `ServiceDefID = S` | A service other than S requiring the Duty is processed | The rule does not apply |
| 2.0 | A Duty Rule with `ServiceDefID = S` and a duty-global rule for the same Duty | A service S requiring the Duty is processed | Only the per-service rule applies; the global rule is overridden for that pair, per B8 |
| 2.0 | A Duty Rule whose `CriteriaValue` does not match its criterion's grammar | The rule is created | The write succeeds; at evaluation, the rule produces no candidates and the run continues, per B1 |
| 2.0 | A Duty Rule whose `CriteriaType` is outside the vocabulary | The rule is created | The write is rejected with `ValidationFailed` |

### 3.0 Manage Membership

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 3.0 | A new Member with all required fields | The Member is created | The row exists; `MemberStatusID` references the configured default status |
| 3.0 | A Member with a ReceiptNumber and no CardNumber, and the configured duration has elapsed | A card is issued | A CardNumber is set on the Member; the receipt's IdentifierHistory row is retired with `UnassignedDate` and a reason; the card's IdentifierHistory row is created |
| 3.0 | A Member with a CardNumber | The Member's status is set to Deceased | The CardNumber remains; it is not reassigned |
| 3.0 | A Member with a MemberAttributeValue | The same attribute is set again | The existing value is replaced; the (MemberID, AttributeTypeID) uniqueness constraint is preserved |
| 3.0 | A readmission recorded for a member | The readmission is recorded | A Readmission row exists; the Member's MemberStatusID is not modified |
| 3.0 | A member with two Readmission rows | The readmission count is requested | The count is 2; no stored field carries the count |
| 3.0 | A Readmission row | A soft delete is requested | No soft-delete operation exists; Readmission carries no lifecycle flags, per B14 |

### 4.0 Manage Eligibility

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 4.0 | A grant with `GrantedDate <= today` and `RevokedDate = null` | Eligibility is resolved | The member is eligible |
| 4.0 | A grant with `GrantedDate > today` and `RevokedDate = null` | Eligibility is resolved | The member is not eligible; a future grant does not confer current eligibility |
| 4.0 | A grant with `GrantedDate <= today` and `RevokedDate > today` | Eligibility is resolved | The member is eligible |
| 4.0 | A grant with `GrantedDate <= today` and `RevokedDate <= today` | Eligibility is resolved | The member is not eligible |
| 4.0 | Two grants for the same pair, with different GrantedDates, both current | Eligibility is resolved | The one with the later GrantedDate is used; ties are broken by the greater EligibilityID |
| 4.0 | A revoked grant | A revoke is requested again | The operation is rejected with `Conflict` |

### 8.0 Manage Events and Programs

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 8.0 | An Event with a HostBranchID and two EventBranch rows | The Event is created | The HostBranch need not appear in EventBranch; both host and attendee sets are stored independently, per B20 |
| 8.0 | An Event with no EventBranch rows | The Event is created | The Event exists; its roster scope is empty |
| 8.0 | An Event and a duplicate EventBranch row for the same branch | The EventBranch is created | The composite primary key (EventID, BranchID) rejects the duplicate |
| 8.0 | A ProgramItem with a ServiceDefID | The ProgramItem is created | An event-sourced ServiceOccurrence is created with `GeneratedBy = Administrator`, `EventID` set, `ScheduleID` null |
| 8.0 | A ProgramItem whose ServiceDefID is removed after an occurrence was created | The ProgramItem is updated | The event-sourced ServiceOccurrence is not removed |
| 8.0 | A second Program for the same Event | The Program is created | The operation is rejected with `Conflict`; one Program per Event |
| 8.0 | An EventDuty referencing a Duty | The EventDuty is created | The duty's Eligibility and DutyRule are consulted for candidate resolution on the event |
| 8.0 | A Member whose `MemberStatusID` is a non-rosterable status, referenced by an EventDuty | The EventDuty is retained | The reference remains; no cascade; the EventDuty.AssignedMemberID continues to point at the Member |

## Operations layer

### 5.0 Generate Assignment

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 5.0 | An occurrence with two required slots | 5.0 runs | Two assignments are created with `AssignmentStatusID = NULL`, `AssignmentSource = Automatic`, `AssignedBy = NULL` |
| 5.0 | An occurrence with two required slots and one already assigned | 5.0 runs | One additional assignment is created; the existing one is not modified |
| 5.0 | An occurrence with two required slots and both filled | 5.0 runs | No assignment is created; the run succeeds |
| 5.0 | A duty with no eligible members | 5.0 runs | No assignment is created for that duty; the duty is counted as unfilled |
| 5.0 | A duty with three eligible members and one slot | 5.0 runs | The member with the lowest MemberID is assigned, per B5 |
| 5.0 | A duty with five eligible members and three slots | 5.0 runs | The three members with the lowest MemberIDs are assigned, per B5 |
| 5.0 | A duty with a tier-1 criterion that no eligible member matches, and a tier-2 criterion that two do | 5.0 runs | The two tier-2 members are candidates; tier 1 does not fall back |
| 5.0 | A duty whose only rules are reserved criteria types | 5.0 runs | No candidate is produced for any tier; the duty is unfilled |
| 5.0 | A member already holding a non-terminal assignment on the occurrence | 5.0 runs | The member is not a candidate for any other duty on the same occurrence |
| 5.0 | A member whose only assignment on the occurrence is terminal | 5.0 runs | The member is a candidate for other duties on that occurrence |
| 5.0 | Two 5.0 runs executing concurrently on the same occurrence | Both runs complete | The non-terminal assignment count for any (OccurrenceID, DutyID) never exceeds the effective required slot count, per B6; the count is evaluated against the committed state |
| 5.0 | A 5.0 run and a concurrent 12.0 manual assignment on the same occurrence | Both complete | The capacity invariant holds; the manual assignment is either counted toward the effective required count or the 5.0 run does not exceed it |
| 5.0 | A (MemberID, DutyID, OccurrenceID) triple already exists | 5.0 attempts to insert a second row for the same triple | The insert is rejected by the uniqueness constraint; the run continues |
| 5.0 | Two 5.0 runs executing sequentially on the same occurrence | The second run completes | No new assignments are created; the run is idempotent with respect to already-filled slots |
| 5.0 | An interrupted 5.0 run | 5.0 runs again | The interrupted run's partial rows are counted; remaining slots are filled; no checkpoint is required |
| 5.0 | A duty whose ServiceOccurrenceDuty row has `Action = Removed` | 5.0 runs | The duty is not filled for that occurrence |
| 5.0 | A duty whose ServiceOccurrenceDuty row has `Action = Added` with a `RequiredSlotCount` | 5.0 runs | The override count is used |
| 5.0 | A Branch-Attendance Recency rule on a duty, with the effective register scope on and a matching AttendanceRecord | 5.0 runs | The member is a candidate |
| 5.0 | A Branch-Attendance Recency rule on a duty, with the effective register scope off | 5.0 runs | The rule is skipped; the tier evaluates on its other criteria, or produces every eligible member if it had no other criteria |
| 5.0 | A 9.0 invocation failing during the run | 5.0 completes | The assignment is committed; the failure is logged; the run is not marked as failed; no retry occurs, per B7 |
| 5.0 | An occurrence with three duties, two slots each | 5.0 runs | Six assignments at most; each duty's fill count is independent |
| 5.0 | An event-sourced occurrence | 5.0 runs | No duty list is inherited from a schedule; only ServiceOccurrenceDuty-added duties are filled, if any |

### 6.0 Record Attendance

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 6.0 | A member and an occurrence with no existing attendance record | Attendance is recorded | One AttendanceRecord row exists with `Source = Manual` |
| 6.0 | A member and an occurrence with an existing attendance record | Attendance is recorded again | No second row is created; the existing record is returned |
| 6.0 | A member with neither ReceiptNumber nor CardNumber | Attendance is recorded | The operation is rejected with `ValidationFailed`; attendance is not recorded for members below the issuance age |
| 6.0 | A member with a ReceiptNumber | Attendance is recorded | The record is written |
| 6.0 | An occurrence at a branch whose effective register scope is off | Attendance is recorded | The operation is rejected with `ConfigurationMissing`; no row is written |
| 6.0 | An occurrence at a branch whose effective register scope is on | Attendance is recorded | The row is written |
| 6.0 | A member attending at a branch other than their home branch | Attendance is recorded | The row is written against the occurrence; Member.BranchID is not consulted |
| 6.0 | A member with no RosterAssignment for the occurrence | Attendance is recorded | The row is written; assignment is not a precondition |
| 6.0 | An occurrence dated in the future | Attendance is recorded | The row is written; there is no attendance-window enforcement |
| 6.0 | A missing member or occurrence | Attendance is recorded | The operation is rejected with `NotFound` |

### 7.0 Manage Confirmation

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 7.0 | An assignment with `AssignmentStatusID = NULL` | Confirm is received | `AssignmentStatusID` is set to the status named by `InitialAssignmentStatusID` |
| 7.0 | An assignment with `AssignmentStatusID = NULL` | Decline is received | `AssignmentStatusID` is set to `DeclinedStatusID`; if the status is terminal, re-resolution runs |
| 7.0 | An assignment with `AssignmentStatusID = NULL` past the timeout | The sweep runs | `AssignmentStatusID` is set to `TimedOutStatusID`; if terminal, re-resolution runs |
| 7.0 | An assignment whose elapsed age is below `ConfirmationTimeoutHours` | The sweep runs | No transition occurs |
| 7.0 | A terminal assignment | A non-terminal transition is requested, and the slot is full | The operation is rejected with `LifecycleViolation`, per B3 |
| 7.0 | A terminal assignment | A non-terminal transition is requested, and the slot has room | The transition is permitted |
| 7.0 | A terminal-to-terminal transition | The transition is applied | No capacity check is performed |
| 7.0 | A decline with a terminal status and one candidate available | Decline is processed | A replacement assignment is created with `AssignmentStatusID = NULL`, `AssignmentSource = Automatic`, `AssignedBy = NULL` |
| 7.0 | A decline with a terminal status and no candidate available | Decline is processed | The slot remains vacant; no fallback; the result counts one vacancy |
| 7.0 | A member who previously declined the slot | Decline is processed again | The member is excluded from the replacement candidate set |
| 7.0 | A member who previously timed out on the slot | Decline is processed again | The member is excluded from the replacement candidate set |
| 7.0 | An occurrence dated before today in the configured ApplicationTimeZone, with a NULL assignment past the timeout | The sweep runs | The transition is recorded; no replacement is sought; no notification is sent |
| 7.0 | `InitialAssignmentStatusID` is absent | Confirm is requested | The run is refused with `ConfigurationMissing` |
| 7.0 | `DeclinedStatusID` is absent | Decline is requested | The operation is refused with `ConfigurationMissing`, per B4 |
| 7.0 | `TimedOutStatusID` is absent | The sweep runs | The sweep is refused with `ConfigurationMissing`, per B4 |
| 7.0 | An assignment whose target status is non-terminal | Decline is processed | No re-resolution occurs; the slot remains occupied |
| 7.0 | A replacement is created | The replacement's 9.0 invocation fails | The replacement remains committed; the failure is caught and logged; no retry |
| 7.0 | A 7.0 decline and a concurrent 5.0 run targeting the same slot | Both complete | The capacity invariant holds |

### 9.0 Dispatch Notification

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 9.0 | A new automatic assignment created by 5.0 | 5.0 completes the assignment insert | 9.0 is invoked fire-and-forget; the assignment is not dependent on the notification's success |
| 9.0 | A 9.0 invocation that throws | The throw propagates to the caller | 5.0 catches it, logs it, continues; the assignment stands, per B7 |
| 9.0 | `NotificationChannel` absent and `Required = true` | Dispatch is attempted | The invocation refuses and surfaces `ConfigurationMissing` |
| 9.0 | `NotificationChannel` absent and `Required = false` | Dispatch is attempted | No notification is sent; no error |
| 9.0 | `NotificationChannel` set to an unrecognised value | Dispatch is attempted | No notification is sent; no error |
| 9.0 | A ConfigurationAuditLog row with `NotifiedAt = NULL` | The authority sweep runs | The row is processed; recipients are resolved; notifications are dispatched; `NotifiedAt` is set |
| 9.0 | A ConfigurationAuditLog row with `NotifiedAt` set | The authority sweep runs | The row is skipped |
| 9.0 | A dispatch failure on the authority sweep | The sweep continues | `NotifiedAt` remains NULL; the row is eligible for a later sweep, per B15 |
| 9.0 | A dispatch that succeeds but the process fails before `NotifiedAt` is persisted | A later sweep runs | The same audit row may be dispatched again; duplicate dispatch is a permitted outcome, per B15 |
| 9.0 | A NotificationSubscription with `IsActive = false` | The sweep runs | The subscription is not consulted |
| 9.0 | A by-tier subscription whose tier has no non-deleted admins | The sweep runs | Zero recipients; `NotifiedAt` is set; no error, per B16 |
| 9.0 | A by-admin subscription whose admin is soft-deleted | The sweep runs | Zero recipients; `NotifiedAt` is set; no error |
| 9.0 | No subscription matches an audit row's EventType | The sweep runs | Zero recipients; `NotifiedAt` is set; no error |
| 9.0 | Multiple subscriptions match the same audit row | The sweep runs | Each matching subscription produces its own recipient set; the notification is dispatched per resolved recipient |
| 9.0 | The path-1 invocation | The member-facing dispatch runs | The member receives the notice; delivery is not tracked |
| 9.0 | The path-2 authority sweep | The dispatch succeeds | `NotifiedAt` is set on the audit row |

### 10.0 Evaluate Fill Status

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 10.0 | An occurrence with a required count of 2 and 0 active assignments | 10.0 runs | `FillStatusID` is set to the value named by `OutcomeStateUnfilledID` |
| 10.0 | An occurrence with a required count of 2 and 1 active assignment | 10.0 runs | `FillStatusID` is set to `OutcomeStatePartiallyFilledID` |
| 10.0 | An occurrence with a required count of 2 and 2 active assignments | 10.0 runs | `FillStatusID` is set to `OutcomeStateFilledID` |
| 10.0 | An occurrence with a required count of 0 | 10.0 runs | `FillStatusID` is set to `OutcomeStateFilledID`; the zero-duty rule classifies as Filled |
| 10.0 | An occurrence with all terminal assignments and a required count > 0 | 10.0 runs | The terminal assignments are not counted; the state reflects the active count |
| 10.0 | An occurrence whose current `FillStatusID` equals `OutcomeStateCancelledID` | 10.0 runs | The occurrence is skipped; the count is recorded separately |
| 10.0 | `OutcomeStateCancelledID` is absent | 10.0 runs | No exclusion applies; the occurrence is evaluated |
| 10.0 | Any of the three required mapping settings absent | 10.0 runs | The whole run is refused with `ConfigurationMissing`; no occurrence is evaluated |
| 10.0 | An occurrence already evaluated with no state change | 10.0 runs again | The same `FillStatusID` is written |
| 10.0 | A range invocation from `FromDate` to `ToDate` | 10.0 runs | Every occurrence with `Date` in the range is evaluated |
| 10.0 | A single-occurrence invocation | 10.0 runs | Only that occurrence is evaluated |
| 10.0 | Neither occurrence nor range supplied | 10.0 runs | The run is refused with `ValidationFailed` |
| 10.0 | Both a single occurrence and a range supplied | 10.0 runs | The run is refused with `ValidationFailed` |
| 10.0 | A concurrent 5.0 write to an occurrence being evaluated | Both complete | The result reflects whichever committed state 10.0 read; 10.0 does not modify assignments |

### 11.0 Materialize Occurrences

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 11.0 | One active weekly schedule, a 30-day horizon, and a fixed "today" of 2026-10-06 | 11.0 runs | Occurrences are created for every candidate date in the horizon, inclusive of today and today + 30 |
| 11.0 | An active schedule whose time slot is on Sunday, and a horizon covering four Sundays | 11.0 runs | Four occurrences are created on those Sundays |
| 11.0 | An active schedule whose definition is inactive | 11.0 runs | The schedule is not picked up; no occurrences are created for it |
| 11.0 | An active schedule whose time slot is inactive | 11.0 runs | The schedule is not picked up |
| 11.0 | A prior run of 11.0 that created four occurrences | 11.0 runs again | No new occurrences are created; the total remains four |
| 11.0 | A schedule whose ServiceDefinition has zero duties | 11.0 runs | Occurrences are still created; the zero-duty case does not block materialization |
| 11.0 | `OccurrenceHorizonDays` absent | 11.0 runs | The run fails with `ConfigurationMissing`; no occurrences are created; a MaterializerRun row records the failure |
| 11.0 | `OccurrenceHorizonDays = 0` | 11.0 runs | Only today's candidate dates are materialized |
| 11.0 | A Manual trigger with `TriggeredByAdminId = null` | 11.0 runs | The command is rejected with `ValidationFailed` |
| 11.0 | A Scheduled trigger with a non-null `TriggeredByAdminId` | 11.0 runs | The command is rejected with `ValidationFailed` |
| 11.0 | A manual horizon override smaller than the existing coverage | 11.0 runs | No new occurrences are created; the run succeeds |
| 11.0 | A manual horizon override larger than the standing setting | 11.0 runs | Additional occurrences are created up to the override horizon |
| 11.0 | An existing schedule-sourced occurrence for a candidate (ScheduleID, Date) | 11.0 runs | The existing occurrence is not touched; no update occurs |
| 11.0 | A prior run and a concurrent run contending for the advisory lock | Both complete | One run acquires the lock and proceeds; the other returns Failure with "Another materializer run is in progress" |
| 11.0 | A manual trigger during a scheduled run | Both complete | The advisory lock serializes them; no duplicate occurrences are created |
| 11.0 | An existing occurrence created as event-sourced | 11.0 runs | The event-sourced occurrence is not touched; 11.0 does not read or modify it |
| 11.0 | An existing ServiceOccurrenceDuty override on a materialized occurrence | 11.0 runs | The override is not modified |
| 11.0 | A MaterializerRun record from a prior successful run | 11.0 runs again | A new MaterializerRun record is written for the new run |
| 11.0 | A failed run partway through | 11.0 runs again | The partial rows from the failed run are counted; remaining candidate dates are filled; no checkpoint is required |
| 11.0 | An instant near midnight UTC and a configured non-UTC `ApplicationTimeZone` | 11.0 derives "today" | "Today" resolves to the calendar date in the configured zone, not the UTC date, per C7 |
| 11.0 | `ApplicationTimeZone` set to an unrecognised value | 11.0 derives "today" | The run is refused with `ConfigurationInvalid`; no occurrences are created |
| 11.0 | `ApplicationTimeZone` absent | 11.0 derives "today" | The run is refused with `ConfigurationMissing`; no occurrences are created |

### 12.0 Create Manual Assignment

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 12.0 | A member, duty, occurrence, and acting admin | A manual assignment is created with no target status | An assignment is created with `AssignmentStatusID = NULL`, `AssignmentSource = Manual`, `AssignedBy` set; 9.0 is invoked |
| 12.0 | A member, duty, occurrence, and acting admin | A manual assignment is created with a target status | The row is created with that status; 9.0 is not invoked |
| 12.0 | A manual assignment whose member does not satisfy a Role Duty Rule | The assignment is created | The assignment is permitted; Role criteria are bypassed, per B12's contract |
| 12.0 | A manual assignment whose member does not satisfy an Eligibility Flag rule | The assignment is created | The assignment is rejected with `ValidationFailed`; Eligibility Flag gates manual assignment |
| 12.0 | A manual assignment when the duty is already at its configured slot count | The assignment is created | The assignment is permitted; slot capacity is not enforced on manual assignment |
| 12.0 | A manual assignment when the member already holds a non-terminal assignment on the occurrence | The assignment is created | The assignment is permitted; per-occurrence availability is not enforced on manual assignment |
| 12.0 | The same (MemberID, DutyID, OccurrenceID) triple already exists | A manual assignment is created again | The assignment is rejected with `Conflict` |
| 12.0 | A referenced member, duty, occurrence, admin, or target status that does not exist | A manual assignment is created | The operation is rejected with `NotFound` |
| 12.0 | A manual assignment with no target status | The assignment is created | 9.0 is invoked for the created row |

### 13.0 Attendance Rule Engine

| Process | Given | When | Then |
| --- | --- | --- | --- |
| 13.0 | A rule with `TriggerType = AbsenceDays`, `TriggerValue = 90`, and a member absent 91 calendar days | The scheduled run executes | The trigger fires; the configured outcome is applied |
| 13.0 | A rule with `TriggerType = AbsenceDays`, `TriggerValue = 90`, and a member absent exactly 90 calendar days | The scheduled run executes | The trigger does not fire; the window is `[today - 90, today]` inclusive |
| 13.0 | An `AbsenceDays` rule and an occurrence-attendance record on `today - 90` | The scheduled run executes | The trigger does not fire |
| 13.0 | A member whose effective register scope is off | The scheduled run executes | The member is skipped for absence-based triggers |
| 13.0 | Two rules with different scope criteria, both matching a member | The scheduled run executes | Both rules are evaluated in ascending AttendanceRuleID order; earlier outcomes are visible to later ones, per B8 |
| 13.0 | Two `SetStatus` rules for the same member on the same run | The scheduled run executes | The later rule's status wins; the write occurs after the earlier one, per B8 |
| 13.0 | A `SetStatus` rule and a `Notify` rule on the same member | The scheduled run executes | Both take effect |
| 13.0 | Two `Notify` rules on the same member | The scheduled run executes | Two notifications are produced; no deduplication |
| 13.0 | A rule that fires on run 1 and whose trigger still holds on run 2 | The second scheduled run executes | The outcome is applied again; the engine is not idempotent with respect to external outcomes, per B10 |
| 13.0 | A rule with `TriggerType = Manual` | A scheduled run executes | The rule is not evaluated |
| 13.0 | A rule with `TriggerType = Manual` and a specific rule ID | The rule is invoked administratively | Only the named rule is evaluated; its scope is resolved against the population; every matching member receives the outcome, per B11 |
| 13.0 | A rule with `TriggerType = Manual` and no valid `ActingAdminID` | The rule is invoked administratively | The invocation is rejected with `NotFound`; no outcome is applied |
| 13.0 | A rule with `TriggerType = Manual` and `TriggerValue` set | The rule is invoked | `TriggerValue` is ignored; the Manual trigger fires unconditionally |
| 13.0 | Two invocations of the same Manual rule on the same day | Both invocations complete | The outcome is applied twice; the engine does not maintain per-rule execution history |
| 13.0 | A rule whose outcome is `SetStatus`, and the target status is already the member's current status | The scheduled run executes | The write is applied; the member's state is unchanged; the persistence layer's optimisation of identical writes is not part of the contract |
| 13.0 | A rule with `TriggerType = ReadmissionCount`, `TriggerValue = 3`, and a member with 3 Readmission rows | The scheduled run executes | The trigger fires |
| 13.0 | A member with 2 Readmission rows and a `ReadmissionCount = 3` rule | The scheduled run executes | The trigger does not fire |
| 13.0 | The engine | A scheduled run | The engine does not write `Readmission`; readmissions are recorded by 3.0 |
| 13.0 | A member's readmission count | It is requested | The count is the number of Readmission rows for that member; the count is not stored |

## Cross-cutting invariants

| Area | Given | When | Then |
| --- | --- | --- | --- |
| Temporal | A timestamp column written by any process | The row is persisted | The value is UTC |
| Temporal | A date column written by any process | The row is persisted | The value is the calendar date the caller supplied; no conversion through UTC |
| Temporal | A time column written by any process | The row is persisted | The value is the wall-clock time the caller supplied |
| Temporal | `ApplicationTimeZone = Africa/Johannesburg` and an instant at 22:30 UTC | The date is derived | The result is the next calendar day in Johannesburg |
| Configuration | A configuration change (create, update, deactivate, soft delete) | Any process runs afterward | The change takes effect on that run; historical records are not rewritten |
| Configuration | A configuration entity | Both flags are inspected | `IsActive` and `IsDeleted` reflect one of the four reachable states; the forbidden combination does not occur |
| Configuration | A configuration deactivation or soft delete | The operation executes | A ConfigurationAuditLog row is written with reason, initiated-by, approved-by, and timestamps |
| Configuration | A configuration soft delete whose consequences the admin has not acknowledged | The operation executes | The operation is refused; the consequences preview must be acknowledged |
| Configuration | Any configuration change | Any operational record exists | The operational record is unchanged |
| Invocation | Any process other than 5.0, 7.0, 12.0, or 13.0 | Another process invokes it | The invocation is not permitted; the invocation graph is acyclic and terminates at 9.0 |
| Invocation | 9.0 | An invocation arrives | 9.0 is the sole process any other process is permitted to invoke |
| Invocation | 9.0's authority-notification sweep | The scheduler triggers it | It is a trigger, not an invocation; no process calls 9.0 for it |
| Deletion | A configuration entity with references | A soft delete is applied | The reference fallback and cascade stated in §9.7 is applied atomically |
| Deletion | A member | A soft delete is applied | The member is hidden from views; all operational records naming the member remain |
| Deletion | A Role that is the default Role | A soft delete is requested | The operation is blocked |

## Boundary conditions

The matrix tests each closed decision's boundaries explicitly. The list below names the boundaries, so an implementer can confirm each is exercised.

| Decision | Boundary tested |
| --- | --- |
| B1 | A `CriteriaValue` at the edge of its grammar (e.g., `13-35`, `13 - 35`, `Purity=Pure`, `Purity = Pure`, `2,3,4`, `2, 3, 4`) |
| B2 | YouthAgeMin and YouthAgeMax exactly inclusive |
| B3 | terminal → non-terminal at and around the slot-capacity threshold |
| B4 | Each of the three target settings absent |
| B5 | Ascending-MemberID tie-break with equal-tier candidates |
| B6 | Concurrent committed state, not intermediate transient states |
| B7 | 9.0 invocation throwing and timing out |
| B8 | Rule order and state visibility between rules |
| B10 | Repeated run with a still-holding trigger |
| B11 | Manual invocation of one rule and only one |
| B12 | `IncrementReadmissionCount` absent from the outcome vocabulary |
| B13 | 90 and 91 calendar days absence |
| B14 | Readmission carries no lifecycle flags |
| B15 | Dispatch failure and post-dispatch, pre-persist failure |
| B16 | Zero recipients and inactive subscription |
| B17 | External invocation outside the atomic unit |
| B18 | Both `ScheduleID` and `EventID` null; both non-null |
| B19 | Cross-branch schedule mismatch |
| B20 | Host not in EventBranch; EventBranch empty |

## How to use this matrix

Each row is a test case. Each test case is independent of the implementation's mechanism. The implementation may use any mechanism that satisfies the row.

A row is not a scenario script. It is a condition, an action, and an expected result. A test author writes the script; the matrix states what the script must verify.

Where a row states a property rather than a specific value, the test verifies the property. Where a row states an exact value, the test verifies that value.

Where a row is a negative — "no row is written," "no transition occurs," "no retry happens" — the test asserts the absence of the effect, not merely the presence of a different effect.

---

*Source: System Design Specification §7–§17; the closed Class B decisions; the process contracts.*
