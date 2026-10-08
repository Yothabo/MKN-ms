# Errors

*The system-wide error taxonomy. Derived from the System Design Specification §9–§17 and the process contracts. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states the semantic categories of errors the system produces, what each category means, and whether the process returns it as a Failure result or throws it.

The categories are semantic. They are not HTTP statuses. An HTTP adapter maps each category to a status; the mapping is the adapter's concern, not the specification's.

---

## Categories

### ValidationFailed

**Meaning.** The input does not satisfy the stated rules. A required field is missing. A field has the wrong type. A field's value is outside the permitted range. A pair of fields is contradictory.

**Behaviour.** Returned as a Failure result. The result carries the failing field name and a description of the rule that was violated.

**Examples.** `Operation` supplied as neither "Confirm" nor "Decline." `SequenceOrder` negative. `TriggerType` outside the fixed vocabulary.

### NotFound

**Meaning.** A referenced entity does not exist.

**Behaviour.** Two cases.

- When the entity is a *command input* — the caller supplied an ID for an entity the command references — the process returns a Failure result.
- When the entity is the *target* of the operation — the service was asked to operate on a missing row that the caller should have provided — the process throws `EntityNotFoundException`.

**Examples.** A MemberID supplied in a CreateManualAssignment command that names no member: Failure. A RoleID supplied in a RenameRole operation that names no role: thrown.

### Conflict

**Meaning.** A uniqueness or state constraint is violated.

**Behaviour.** Returned as a Failure result.

**Examples.** A manual assignment for a (MemberID, DutyID, OccurrenceID) triple that already exists. A Program created for an Event that already has one. A Role that is already inactive when deactivation is requested.

### LifecycleViolation

**Meaning.** A permitted-transition rule rejected the operation. The entity exists; the requested transition is not permitted from its current state.

**Behaviour.** Returned as a Failure result.

**Examples.** A terminal-to-non-terminal transition that would exceed the effective required slot count for the affected (OccurrenceID, DutyID) pair.

### ConfigurationMissing

**Meaning.** A required `SystemSetting` referenced by the process is absent.

**Behaviour.** Returned as a Failure result. The process refuses to run. This is the required-setting rule stated in §2.

**Examples.** `OccurrenceHorizonDays` absent when 11.0 runs. `InitialAssignmentStatusID` absent when 7.0 runs. `OutcomeStateUnfilledID` absent when 10.0 runs.

### ConfigurationInvalid

**Meaning.** A required `SystemSetting` is present but its value is unparseable, or it points at a row that does not exist.

**Behaviour.** Returned as a Failure result.

**Examples.** `OccurrenceHorizonDays` present but not a non-negative integer. `InitialAssignmentStatusID` present but not a valid AssignmentStatusID.

### ConcurrencyConflict

**Meaning.** A concurrent write prevented the process from preserving a required invariant.

**Behaviour.** Returned as a Failure result, or thrown, depending on the mechanism. If a process detects that it cannot satisfy the invariant — for example, an advisory lock could not be acquired, a serialization failure occurred — it either returns a Failure naming the conflict or throws. The mechanism is implementation choice per B6.

**Examples.** Two materializer runs contended for the advisory lock. Two 5.0 runs contended for the same slot.

### DependencyFailure

**Meaning.** An external dependency could not be reached. A notification transport failed. A scheduled job's infrastructure is unavailable.

**Behaviour.** Depends on the process.

- A member-facing notification invocation that fails is caught and logged by the caller; the assignment stands. Not surfaced as a Failure.
- An authority-notification dispatch that fails leaves `NotifiedAt` null and the row is retried on a later sweep. Not surfaced as a Failure of the sweep.
- Any other external dependency failure is a system fault and is thrown.

**Examples.** The configured notification channel is unreachable during a member-facing dispatch. The database connection drops mid-run.

### InvariantViolation

**Meaning.** An invariant that should be impossible to violate has been violated. This is a defect, not a caller error.

**Behaviour.** Thrown. Not returned as a Failure. The caller's error handler surfaces the exception; the process does not attempt recovery.

**Examples.** A `RosterAssignment` row written with both `ScheduleID` and `EventID` null (the B18 invariant). A `ServiceOccurrence` row for a schedule-sourced occurrence with a null `ScheduleID`. A `Program` row that is the second one for the same event.

---

## What is not in this taxonomy

**No HTTP statuses.** The categories are semantic. A `ValidationFailed` might map to HTTP 400, and a `Conflict` to HTTP 409, and a `NotFound` to HTTP 404, in a REST adapter. That mapping belongs to the adapter.

**No transport-level errors.** A network timeout, a DNS failure, a database connection loss — those are handled by the runtime and by the process that experiences them. They are not part of this semantic taxonomy unless the process chooses to surface them as a DependencyFailure.

**No authorisation errors.** The specification assumes the caller has passed the applicable authority check. The process receives an `AdminID` that is presumed authorised. If the process receives an `AdminID` that does not resolve to a non-deleted `Admin` row, that is a `NotFound` for the command input, not an authorisation failure.

---

## The relationship to `ConfigurationErrorCodes`

The existing code in the API layer uses a smaller set of codes (`ConfigurationErrorCodes`) for the configuration processes. That set is a subset of the taxonomy in this document. Specifically:

| Existing code | Corresponds to |
| --- | --- |
| not_found | NotFound |
| duplicate_name | Conflict |
| invalid_value | ValidationFailed |
| referenced_entity_inactive | NotFound |
| referenced_entity_missing | NotFound |
| already_inactive | Conflict |
| already_active | Conflict |

When the implementation is brought to the frozen specification, the configuration-layer codes either expand to cover the taxonomy or remain a smaller refinement of it. The taxonomy in this document is the authority; the codes are an implementation detail.

---

## Summary

| Category | Returned or thrown | When it applies |
| --- | --- | --- |
| ValidationFailed | Returned | Input does not satisfy stated rules |
| NotFound | Returned for command input; thrown for target entity | A referenced entity does not exist |
| Conflict | Returned | A uniqueness or state constraint is violated |
| LifecycleViolation | Returned | A permitted-transition rule rejected the operation |
| ConfigurationMissing | Returned | A required setting is absent |
| ConfigurationInvalid | Returned | A required setting is present but invalid |
| ConcurrencyConflict | Returned or thrown | A concurrent write prevented an invariant from holding |
| DependencyFailure | Thrown, or caught and logged | An external dependency could not be reached |
| InvariantViolation | Thrown | A should-be-impossible condition occurred |

---

*Source: System Design Specification §2, §9, §10, §11, §12, §13, §14, §16, §17; the process contracts.*
