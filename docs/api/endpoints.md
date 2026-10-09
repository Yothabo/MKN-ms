# Endpoints

*The endpoint list, grouped by the process each endpoint serves. Derived from `adapter-contract.md` and `../processes/contracts.md`; where this document conflicts with the adapter contract or the specification, they win.*

---

## Status

The technology stack is chosen: ASP.NET Core Web API, JSON request and response bodies. The authoritative adapter contract is `adapter-contract.md`. The endpoint list below reflects the process exposure the system defines.

---

## What will live here

This file lists the endpoints the system exposes, grouped by the process each endpoint serves.

### Configuration endpoints

- Configure Vocabulary (1.0)
- Configure Duty Rules (2.0)
- Manage Membership (3.0)
- Manage Eligibility (4.0)
- Manage Events and Programs (8.0)

### Operations endpoints

- Generate Assignment (5.0)
- Record Attendance (6.0)
- Manage Confirmation (7.0)
- Evaluate Fill Status (10.0)
- Materialize Occurrences (11.0)
- Create Manual Assignment (12.0)
- Attendance Rule Engine (13.0) — manual single-rule invocation. Specified, not yet implemented.

The Dispatch Notification process (9.0) is invoked internally by 5.0, 7.0, 12.0, and 13.0, and carries its own scheduled authority-notification sweep. It has no externally exposed endpoint.

---

## The one-endpoint-one-process rule

Each endpoint invokes exactly one process or subprocess. No endpoint stitches together behavior from multiple processes.

An endpoint may invoke:

- A parent process (e.g., 5.0 Generate Assignment), which runs its subprocesses internally.
- Or a specific subprocess (e.g., 5.1 Filter Eligible Candidates), if the API design chooses to expose subprocesses separately.

What an endpoint may not do:

- Chain multiple processes into one endpoint.
- Introduce behavior that no single process defines.
- Cause a process to invoke another process outside the four locked edges (5.0 → 9.0, 7.0 → 9.0, 12.0 → 9.0, and 13.0 → 9.0).

This rule is what keeps the API surface a faithful presentation of the process model. Any endpoint that appears to break it is either mis-labelled (it should be labelled as invoking one process) or is introducing behavior the system does not have.

---

## Constraints the endpoint list will respect

- **One endpoint, one process (or subprocess).** Stated above.
- **No invented behavior.** Endpoints cannot introduce writes outside the process's store footprint.
- **No new invocation edges.** Endpoints trigger processes; they do not cause processes to invoke each other beyond the four locked edges.
- **Required-setting errors are surfaced.** An endpoint that triggers a process depending on a required setting reports the configuration error when the setting is absent.

---

## Format (proposed)

When populated, each endpoint will be listed with:

| Field | Content |
| --- | --- |
| Method | GET / POST / PUT / DELETE (or equivalent) |
| Path | The endpoint path |
| Process | The process or subprocess it invokes |
| Purpose | One-line description |
| Input | Parameters / body |
| Output | Response shape |
| Errors | Enumerated error conditions |

The specific format will be finalised once the stack is chosen.

---

## Cross-references

- **`process-mapping.md`** — the anticipated process exposure, from which the endpoint list will be derived.
- **`request-response.md`** — the request/response conventions.
- **`../processes/`** — the processes the endpoints expose.
- **Specification §3, §16** — the authoritative sources.

---

*Source: System Design Specification §2, §16. Populated after stack selection.*
