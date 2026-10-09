# Specification Freeze

**Frozen at commit:** `5601ed5` (tag `spec-v1.0-freeze`)
**Frozen on:** 2026-10-08

---

## What is frozen

The specification at `docs/spec/system-design-spec.md` is the authority for the system. It is frozen at the commit named above. From this point forward, the specification is what the implementation is built from.

The derived documents under `docs/architecture/`, `docs/database/`, `docs/processes/`, and `docs/api/` are subordinate to the specification. Where they conflict with the specification, the specification wins.

## What this means for implementation

Implementation proceeds against the frozen specification only. Every entity, every column, every process, every rule in the frozen specification is a contract. Nothing is added to the implementation that is not in the specification. Nothing in the specification is left unimplemented except where the specification itself marks a feature as deferred.

The following are explicitly deferred by the specification and are not to be implemented at this phase:

- The tap ingestion path. `AttendanceRecord.Source` defines the value `Tap`, but no process produces it. Manual register marking is the only implemented attendance source.
- The capability matrix. `Capability` names actions; no assignment of capabilities to roles, tiers, or admins is modelled.
- Notification templates as entities. Message content is composed in code.
- Per-member channel preferences. A single global channel is used.
- The permission model's enforcement. The specification assumes callers have passed the applicable authority check, but the check itself is not defined by the specification.
- The attendance-driven status transitions are implemented, but the youth exemption and any other scope-based exemption is expressed as separate Attendance Rules the administrator configures. The system does not hardcode any exemption.

## What a change to the specification requires

Any change to the frozen specification is an amendment. An amendment is:

1. A stated rule, with its source in the specification and the section it affects.
2. Applied as a change to the specification file itself, not to a derived document.
3. Propagated to every derived document that references the affected rule.
4. Recorded as a new commit that names the amendment.

There is no way to change the implementation's behaviour without changing the specification, and there is no way to change the specification without recording the amendment.

## The current amendment history

The specification was frozen after three amendment sets were applied:

- The initial schema and amendment set (§15.1 through §15.7).
- The A–G amendment set (§15.8.1 through §15.8.18). Member Status, attributes, event restructuring, the two-flag lifecycle, the configuration audit log, the deletion policy, notification subscriptions, the Capability vocabulary.
- The attendance amendment set (§15.8.19 through §15.8.26). The register scope, the Attendance Rule entity, the Readmission entity, and the thirteenth process.

Earlier corrections — the invocation-versus-triggering distinction, the past-occurrence guard, the slot-capacity property statement, the eligibility rule alignment — were folded into the specification before the freeze and are recorded in the git history.

## The conformance position

The specification is conformant with the brain-and-organs architectural model. The configuration layer is the sole authority for organisational policy. The processes execute mechanical operations against configured values and operational data. Fixed system behaviour remains distinguishable from administrator-configured behaviour. The one flagged conformance item — the invocation-versus-triggering distinction — was normalized before the freeze.

## The next phase

Implementation begins. The first work is to bring the current code base into line with the frozen specification. The code at the freeze commit implements twelve processes; the specification defines thirteen. The additions are:

- The Attendance Rule Engine (13.0) and its entities.
- The `AttendanceRecord.Source` column.
- `Branch.UsesAttendanceRegister` and `Event.UsesAttendanceRegister`.
- The attendance register scope resolution.
- The changes from the A–G amendment set that have not yet been brought into the code.

Implementation is scoped to those additions and to any existing code that has drifted from the specification.

---

*This file records the freeze. It is not itself part of the specification. The specification remains the authority.*
