# Decisions

*The closed Class B and Class C decisions, as one-line pointers. Each entry names the decision, states it in one sentence, and points at the section of the tree that carries it. This file adds no rules; it is an index.*

---

## Class B decisions

Class B decisions were made during the documentation-hardening pass. Each closes a question the specification left open. The decision is stated in the specification or in a derived document; this table is a pointer. B9 is a corollary of B8, not a separate decision.

**Note on label collisions.** The specification's §17.1 carries its own local lettered sub-decisions (A through H, with numeric suffixes such as §17.1 B1 through B5 and §17.1 C1 through C4). These are local to §17.1 and are distinct from the Class B decisions B1–B20 and Class C decisions C1–C11 in this file. A reference to §17.1 B1 is not a reference to B1 in the table below. Every reference to §17.1's letters is spelled with the §17.1 prefix; every reference to a Class B or Class C decision uses the bare label.

| Label | Decision | Where it lives |
| --- | --- | --- |
| B1 | A `CriteriaValue` that does not match its criterion's grammar produces no candidates and does not fail the run. | Specification §10.1.5 (criteria value grammars); acceptance-matrix.md (2.0 rows) |
| B2 | Youth age bounds are inclusive; `YouthAgeMin <= age <= YouthAgeMax`. | Specification §10.1.5 (age calculation); acceptance-matrix.md (B2 boundary) |
| B3 | A terminal-to-non-terminal transition is permitted only if the slot does not exceed its effective required count. | Specification §11.1.3, §11.3; acceptance-matrix.md (7.0 rows) |
| B4 | `DeclinedStatusID` and `TimedOutStatusID` are required settings; absence refuses the operation. | Specification §11.1.5, §11.1.7, §15.3.9, §15.3.10; acceptance-matrix.md (7.0 rows) |
| B5 | When a tier produces more candidates than slots, selection is by ascending `MemberID`. Fixed; not configurable. | Specification §10.1.6; acceptance-matrix.md (5.0 rows) |
| B6 | The slot-capacity invariant is preserved by some mechanism; the specification requires the property, not the mechanism. | Specification §10.3, §11.3, §10.5; errors.md (ConcurrencyConflict) |
| B7 | A 9.0 invocation failure does not roll back the assignment and does not mark the run failed. | Specification §10.1.8; acceptance-matrix.md (5.0 rows) |
| B8 | Applicable rules are evaluated sequentially in ascending ID order; earlier outcomes are visible to later ones. | Specification §13.6 (rule evaluation order); acceptance-matrix.md (13.0 rows) |
| B9 | B9 is a corollary of B8, not a separate decision. | Specification §13.6 |
| B10 | The Attendance Rule Engine is deterministic but not idempotent with respect to external outcomes. | Specification §13.6 (idempotency); acceptance-matrix.md (13.0 rows) |
| B11 | A Manual rule invocation names exactly one rule and evaluates only that rule. | Specification §13.6 (manual invocation); contracts.md (13.0 command) |
| B12 | There is no `IncrementReadmissionCount` outcome. Readmission counts are derived. | Specification §13.6; acceptance-matrix.md (B12 boundary) |
| B13 | The absence window is `[today - N, today]`, inclusive at both boundaries, measured in calendar days. | Specification §13.6; acceptance-matrix.md (B13 boundary) |
| B14 | Readmission carries no lifecycle flags; a Readmission row is never soft-deleted. | Specification §4 (Readmission); acceptance-matrix.md (B14 boundary) |
| B15 | Authority notifications are at-least-once; the member-facing path is fire-and-forget. | Specification §14.1.4, §14.1.5; acceptance-matrix.md (9.0 rows) |
| B16 | Zero recipients is a valid outcome; `NotifiedAt` is set and no error is raised. | Specification §14.1.9; acceptance-matrix.md (B16 boundary) |
| B17 | An external process invocation is never part of a database atomic unit. | Specification §9.8; contracts.md §C5 |
| B18 | A `ServiceOccurrence` must carry exactly one of `ScheduleID` or `EventID`; both-null and both-non-null are forbidden. | Specification §4 (Service Occurrence); errors.md (InvariantViolation) |
| B19 | `ServiceDefinition.OwningBranchID` is a hard invariant; a schedule whose time slot's branch differs is an integrity violation. | Specification §9.1.6; acceptance-matrix.md (1.0 rows) |
| B20 | `HostBranchID` and `EventBranch` are independent sets; the host need not appear in the attendee set. | Specification §4 (Event, Event Branch); acceptance-matrix.md (8.0 rows) |

---

## Class C decisions

Class C decisions are the interface and implementation contracts that were written during the same pass. Some have distinct labels in the tree; some do not, and are pointed to by content. Each entry states what the decision is and where it lives.

| Label | Decision | Where it lives |
| --- | --- | --- |
| C1 | Three layers: the HTTP adapter translates, the process applies business rules, the persistence layer writes. No business logic in the adapter. | adapter-contract.md (division of responsibility) |
| C2 | The adapter validates request shape before invoking the process. Required fields, types, dates, and enumerated values are checked at the boundary; a shape failure produces `ValidationFailed` and the process is not invoked. | adapter-contract.md (request shape, request field validation) |
| C3 | Every response is JSON; every response carries a `status` field whose value is `Success` or `Failure`; failure envelopes carry `errorCode` and `errorDetail`. | adapter-contract.md (response shape) |
| C4 | Validation occurs at three boundaries: command shape, referenced entities, outcome invariants. Each boundary validates what it is responsible for and does not duplicate the others. | contracts.md §C4 — Validation rules |
| C5 | Every operation that writes more than one row has an atomic boundary. The specification names which writes form an atomic unit; the mechanism is implementation choice. | contracts.md §C5 — Transaction boundaries; Specification §9.8 |
| C6 | Every process that acts on behalf of an administrator receives an `AdminID` that resolves to an `Admin` row whose `IsActive = true`. The process does not authenticate the caller; authorisation is upstream. | contracts.md §C6 — Identity and context |
| C7 | Timestamps are instants persisted in UTC. Dates are calendar dates with no timezone. Times are wall-clock times with no timezone. `ApplicationTimeZone` is used only to convert an instant to a calendar date. | Specification §4 (Temporal types); acceptance-matrix.md (Temporal rows) |
| C8 | The order in which the frozen specification's schema changes are applied is fixed by dependency. Applying the steps in the stated order leaves the schema in the target state. | migration-ordering.md |
| C9 | The seed scripts are rewritten against the frozen schema, not patched. They are non-authoritative; they configure a starting state for testing. | seed-migration.md |
| C10 | Every error category maps to an HTTP status. The mapping is the adapter's choice; the specification fixes the categories, not the statuses. The line between 400 and 422 is the shape-validation boundary. | adapter-contract.md (error category to HTTP status mapping) |
| C11 | The acceptance matrix states each testable invariant as a Given / When / Then row and names each closed decision's boundary conditions. | acceptance-matrix.md |

---

*Source: the closed Class B and Class C decisions, as recorded in the specification and the derived documents. This file adds nothing; it points.*
