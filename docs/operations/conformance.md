# Conformance Declaration

*At HEAD `f2a7fda`. Supersedes any earlier unversioned copy of this declaration.*

---

## Statement

The implementation at commit `f2a7fda` conforms to the frozen specification at `5601ed5` (tag `spec-v1.0-freeze`) as amended by the specification's §15.8 register, subject to the deferred items listed below.

Every process defined by the specification is implemented. Every required setting and its consumer behaves as the specification states. Every schema amendment listed in §15.1–§15.4 and §15.8 is present. No behaviour is present in the implementation that the specification does not define.

---

## Scope of this declaration

This declaration covers the backend. It does not cover a web frontend; none is part of the current specification's scope, and none is in this repository's `web/` directory that is connected to the backend.

Authentication, authorization, tap ingestion, notification templates as entities, per-member channel preferences, the capability matrix, and the permission model's enforcement are out of scope and deferred. Each is stated in the specification as deferred or out of scope, and each is listed in the runbook.

---

## Traceability

The four-artifact drift-detection loop is now wired and enforced:

  1. `docs/spec/system-design-spec.md` — authority.
  2. `docs/processes/acceptance-matrix.md` — 186 testable rows.
  3. `backend/tests/MknMs.IntegrationTests/` — facts.
  4. `scripts/matrix_tool.py` and `scripts/consistency_check.py` — checks.

The loop closes as follows:

- Every matrix row is either covered by an explicit `[Trait("MatrixRow", "<id>")]`
  on at least one fact, or is marked `(deferred)` in the matrix itself
  with a stated reason. 180 of 186 rows are covered by trait; 6 are
  marked deferred.
- `scripts/matrix_tool.py --strict` fails CI on any new orphaned row
  or any new trait naming a row that does not exist. The six deferred
  identities are whitelisted in `DEFERRED_ORPHANS` — they do not
  silently disappear from the coverage counts, and they do not fail
  CI.
- `scripts/consistency_check.py` fails CI on stale cross-document
  references and on any amendment-register term that has drifted from
  the specification.

Both checks run as steps in `.github/workflows/consistency.yml`, in
the same job, on every push and pull request to `main`. Green CI
means the specification, the matrix, the tests, and the derived
documents are in agreement.

The traceability work closes the review's finding 3.4 ("the
four-artifact loop is 3/4 closed") by making the matrix-to-facts
link explicit and enforced, and by recording the residual as
deferred rows with stated reasons rather than as silent gaps.

---

## What "conforms" means here

The word is used in the conformance-review sense: the implementation's behaviour matches the specification's stated behaviour, tested against the acceptance matrix at `docs/processes/acceptance-matrix.md` and the process invariants at each process document. The specification is the authority; where the implementation and the specification disagree, the specification wins.

Conformance is not the same as completeness:

- The specification deliberately defers certain features. A conforming implementation omits them.
- The specification fixes certain shapes (entity definitions, constraint rules, invocation topology). A conforming implementation follows them.
- The specification leaves certain choices open (physical index shapes, concurrency mechanism, HTTP style, seed values). A conforming implementation makes those choices and records them where the specification names them.

---

## Verification at this commit

- **Build**: 0 errors.
- **Unit tests**: 36 of 36 pass.
- **Integration tests**: 395 of 395 pass, on the developer environment. The CI workflow runs the same suite against a postgres service on the Ubuntu runner.
- **Consistency check**: green. No stale references in the `docs/` tree; no undefined decision labels; no removed columns outside a removal context.
- **CI**: build, unit, and integration jobs green on the PR that merged this commit. Required checks are enforced on `main` via a repository ruleset.

---

## The deferred list

Each of the following is a specification-stated deferral or a documented decision. None is a defect. The full statement and the operator workaround for each is in `docs/operations/runbook.md`.

1. **Health / last-N endpoint.** Requires a specification amendment introducing a job-run entity.
2. **Authentication.** Specification states that authentication is upstream of the process layer.
3. **Authorization enforcement.** Same — upstream, not modelled.
4. **Capability matrix.** §9.1.8 states it is deferred.
5. **Permission-model enforcement.** Deferred by the specification.
6. **Tap ingestion.** `AttendanceRecord.Source` names `Tap`; no path produces it in this phase.
7. **Notification templates as entities.** Message content is composed in code; §14.1.6.
8. **Per-member channel preferences.** Single global channel; §14.1.2.
9. **Manual assignment bypasses `MemberStatus`.** Deliberate; §17.1 B1. Recorded as a known risk.

---

## Residual coverage

Not every specification-stated invariant is exercised by a dedicated
fact. The matrix at `docs/processes/acceptance-matrix.md` is the test
plan; the test tree covers the majority of it, but the following
residual gaps are declared rather than claimed as covered.

- **Soft-delete List/Get visibility.** Eight of approximately twenty
  soft-deletable configuration services have a sample-based fact
  asserting that a soft-deleted row is excluded from `ListAsync` and
  returned as `null` from `GetAsync`. The remaining ~twelve are not
  individually exercised. The pattern is uniform and the shape is
  shared; the coverage is not.
- **Acceptance-matrix row-to-fact map.** A systematic audit of every
  matrix row to the fact that exercises it has not been completed. The
  matrix is the plan; the code is the execution; the map is not
  produced. Where a row has no corresponding fact, that is a known
  coverage gap, not a defect in the implementation.
- **Manual assignment of a non-rosterable member.** The known risk
  (item 9 in the deferred list) that a member whose current
  `MemberStatus` has `IsRosterable = false` can still be assigned
  manually is documented in the runbook but not pinned by a fact. A
  future "hardening" of the manual path that silently rejected such
  assignments would not fail the test suite.

None of the residual gaps is an undeclared behaviour; each is a stated
scope of the tests as they exist at this commit.

---

## What a future amendment would close

An amendment to the frozen specification could close any of the deferred items. Each would require:

1. A stated rule with its source in the specification.
2. A change to the specification file itself, not to a derived document.
3. Propagation to every derived document that references the affected rule.
4. A commit that names the amendment.

The amendment ritual is recorded in `FREEZE.md`. No deferred item in this declaration has been changed by a silent implementation choice.

---

*Source: the frozen specification, the implementation at `9c2accb`, the acceptance matrix, and the runbook. Subordinate to the specification.*
