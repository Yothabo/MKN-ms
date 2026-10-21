# Operations

*Operator-facing documentation. Separate from the specification and from the derived process / architecture / database documents. Where this directory conflicts with the specification, the specification wins.*

---

## Purpose

This directory holds documentation the operator needs to run, observe, and recover the system. It is not a specification document, not a derived design document, and not a process contract. It is what the person operating a deployed instance reads when something is wrong or when they need to know what the system will and will not do for them.

---

## Documents

### `runbook.md`

The operator runbook. Required settings and their consumers, known operational risks, recovery procedures, and the observability limits of the current deployed surface.

---

## What this directory does not contain

- **The specification itself.** That is at `../spec/system-design-spec.md`.
- **The architecture.** That is at `../architecture/`.
- **The process contracts.** Those are at `../processes/`.
- **The database reference.** That is at `../database/`.
- **The API adapter contract.** That is at `../api/`.

---

## Relationship to the specification

The specification is the authority. A runbook entry that names a rule, a setting, or a process contract names it by specification section — not by any label that is local to this directory. The consistency check treats this directory the same as every other `docs/` subdirectory: removed columns do not appear in it, and decision labels appear only where they are defined in `../decisions.md`.

---

*Source: the operator's required knowledge of the deployed system. Subordinate to the specification.*
