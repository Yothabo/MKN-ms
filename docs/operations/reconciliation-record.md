# Reconciliation Record

*The status of every reconciliation item from the post-freeze review, at HEAD `36aa19b`. Supersedes any earlier unversioned copy of this record.*

---

## Purpose

The post-freeze reconciliation review produced a list of items to close before implementation could proceed against the frozen specification. This record states the status of each item at the current HEAD. Items that were closed by commit name the commit; items that were closed by a decision name the decision and where it is recorded; items deferred name the reason.

This is not a specification document. Where this record names a rule, the specification wins.

---

## Items closed by commit

### API reconciliation (5a9cdf2, 72c6302)

The `docs/api/adapter-contract.md` and `docs/api/endpoints.md` were brought into line with the endpoint surface added after the freeze: 13.0's manual invocation, the fifteen soft-delete routes, the Capability and Attribute Type endpoints, Set Member Attribute, Record Readmission, and Notification Subscription. The 9.0 invoker list was corrected from two processes to four in `docs/architecture/invocation-model.md` and in `docs/spec/system-design-spec.md` §16.

### Invocation-model reconciliation (b66d12a, f5164a1)

The invocation model documents and the specification's §10.0 and §11.0 were reconciled with the four-edge invocation topology and the two-path 9.0 model. The durable invariant is stated without a count: 9.0 is the sole process any other process is permitted to invoke.

### Seed-script reconciliation (17112f6, 24bf31c)

`backend/scripts/seed-development.sql` and `backend/scripts/seed-assignment.sql` — both of which referenced pre-amendment schema (Member.IsActive, Event.Location, EventDuty.Label) — were removed and replaced by `seed-vocabulary.sql`, `seed-membership.sql`, and `seed-operations.sql`. The rewrite is documented in `docs/database/seed-migration.md`.

### Consistency-check patterns (5a9cdf2)

Six patterns were added to `scripts/consistency_check.py` to catch drift introduced by the A–G and attendance amendment sets. The check now runs against the whole `docs/` tree and against the hand-maintained amendment register. It is green at HEAD.

### Documentation defects (5a9cdf2)

Two documentation defects — the EventDuty legacy prose and the constraints.md EventDuty foreign-key rows that referenced the pre-amendment shape — were corrected by the same commit.

---

## Items closed by decision

### Item 9 — Specification stale text

Closed by decision during the reconciliation session. The specification is the authority; where a derived document conflicts with it, the derived document is corrected. No specification amendment is required for the stale-text corrections because the specification was not the conflicting document.

### Withdrawn amendments §15.8.39–§15.8.42

Four amendments were drafted and then withdrawn as unnecessary. They are recorded in the commit history (see the specification's §15.8 register), not applied. No action required.

---

## Items deferred

### Health / last-N endpoint (C3)

Deferred. A health endpoint that covers all five scheduled processes requires a persisted job-run record for the four non-11.0 processes — a new entity the specification does not define. Adding the entity is a specification amendment, not an implementation choice. The decision and the operator workaround are recorded in `docs/operations/runbook.md`.

### Identity binding / authentication

Deferred. The specification states that authentication and authorization are upstream of the process layer. Implementing authentication is a specification amendment, not an implementation choice. Recorded as a known risk in `docs/operations/runbook.md`.

### Capability matrix, permission-model enforcement

Deferred by the specification itself. §9.1.8 and the §4 Capability entry state that the assignment of capabilities to roles, tiers, or admins is deliberately deferred. Not implemented.

### Tap ingestion

Deferred by the specification itself. §13.1.4 and §4's `AttendanceRecord.Source` state that the Tap path is defined in the vocabulary but is not produced by any implemented path in the current phase.

### Notification templates as entities, per-member channel preferences

Deferred by the specification itself. §14.1.2 states that the current scope assumes a single global channel; §14.1.6 states that the message content is composed in code. Both are named as future work, not implemented.

### Manual assignment bypasses MemberStatus

Deferred. The behaviour is deliberate — the specification at §17.1 B1 states that manual assignment bypasses non-EligibilityFlag rules — and `MemberStatus.IsRosterable` is not in that bypass list but is not consulted either, because the bypass is over the *Duty Rule* criteria. The behaviour is recorded as a known risk in `docs/operations/runbook.md`, not as a defect.

### Soft-delete visibility coverage

Deferred. Eight services of approximately twenty that expose a soft-delete operation have sample-based visibility tests. The remaining twelve are a coverage gap, not a defect. No action required at this phase; the pattern is established and further classes can adopt it.

---

## The current conformance position

The implementation is conformant with the frozen specification at `36aa19b`, subject to the deferred items above. No item on the deferred list is a defect; each is either a specification-stated deferral or a decision recorded in the runbook.

The conformance declaration for this position is in `docs/operations/conformance.md`.

---

*Source: the post-freeze reconciliation review, its commits, and the decisions recorded in `docs/decisions.md`. Subordinate to the specification.*
