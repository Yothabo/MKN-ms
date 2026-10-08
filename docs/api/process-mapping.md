# Process Mapping

*Anticipated process exposure for the eventual API. Derived from the System Design Specification §3 and §16. Where this document conflicts with the specification, the specification wins.*

---

## Status

This document describes the **anticipated** mapping from the eventual API endpoints to the system's thirteen processes. It is not a finalized API design contract — the API contract does not exist yet, and it will not until the technology stack is chosen.

What this document does establish is the shape of the mapping: every endpoint will invoke exactly one process or subprocess, and no endpoint will introduce behavior beyond what the process defines. Those constraints come from the process model, not from any API design choice, and they hold regardless of stack.

---

## The anticipated mapping

Each process is anticipated to be exposed (or not exposed) through the API according to its trigger and its relationship to external actors.

| # | Process | Trigger | Anticipated exposure |
| --- | --- | --- | --- |
| 1.0 | Configure Vocabulary | Administrator action | Admin-facing endpoints |
| 2.0 | Configure Duty Rules | Administrator action | Admin-facing endpoints |
| 3.0 | Manage Membership | Administrator action | Admin-facing endpoints |
| 4.0 | Manage Eligibility | Administrator action | Admin-facing endpoints |
| 5.0 | Generate Assignment | Scheduled; manual admin | Admin-facing endpoint for manual trigger; also runs on schedule |
| 6.0 | Record Attendance | Member action; manual admin | Member-facing endpoint; admin-facing endpoint for manual entry |
| 7.0 | Manage Confirmation | Member response; scheduled | Member-facing endpoint for response; timeout runs on schedule |
| 8.0 | Manage Events and Programs | Administrator action | Admin-facing endpoints |
| 9.0 | Dispatch Notification | Invoked by 5.0 / 7.0 / 12.0 / 13.0; scheduled authority sweep | Not exposed — internal |
| 10.0 | Evaluate Fill Status | After assignment changes; scheduled | Not exposed — runs internally |
| 11.0 | Materialize Occurrences | Scheduled; manual admin | Admin-facing endpoint for manual trigger; also runs on schedule |
| 12.0 | Create Manual Assignment | Administrator action | Admin-facing endpoint |
| 13.0 | Attendance Rule Engine | Scheduled; manual admin invocation of a single rule | Admin-facing endpoint for manual rule invocation; also runs on schedule |

The term "anticipated" is used deliberately: these are the natural exposures given each process's trigger and external-actor relationship. Whether each is actually exposed, and in what form, is an API design decision still to be made.

---

## The subprocess mapping

Several processes decompose into subprocesses. The API may expose each subprocess separately, or group them behind a single endpoint per process. This is a stack-level and design-level choice.

| Subprocess | Parent process |
| --- | --- |
| 1.1 Configure Role | 1.0 |
| 1.2 Configure Duty | 1.0 |
| 1.3 Configure Branch | 1.0 |
| 1.4 Configure Time Slot | 1.0 |
| 1.5 Configure Service Definition | 1.0 |
| 1.6 Configure Service Duty and Schedule | 1.0 |
| 3.1 Manage Member Record | 3.0 |
| 3.2 Manage Identifier History | 3.0 |
| 5.1 Filter Eligible Candidates | 5.0 |
| 5.2 Order by Priority Tiers | 5.0 |
| 5.3 Fill Occurrence Slots | 5.0 |
| 8.1 Manage Event | 8.0 |
| 8.2 Manage Program | 8.0 |
| 8.3 Manage Program Item | 8.0 |
| 8.4 Manage Event Duty | 8.0 |
| 11.1–11.7 Materialization steps | 11.0 |

The 5.1–5.3 and 11.1–11.7 subprocesses are internal steps of their parent processes. They are anticipated not to be exposed as separate endpoints — the parent process runs them as a single invocation.

---

## The trigger mapping

Each trigger maps to a natural exposure style:

| Trigger | Anticipated exposure |
| --- | --- |
| Administrator action | Admin-facing endpoint |
| Member action | Member-facing endpoint |
| Scheduled execution | Not exposed; runs in the scheduler |
| Manual admin trigger of a scheduled process | Admin-facing endpoint that runs the process on demand |
| Invocation by another process | Not exposed; internal |
| After-assignment-change trigger | Not exposed; runs internally |

The four internal invocation edges (5.0 -> 9.0, 7.0 -> 9.0, 12.0 -> 9.0, and 13.0 -> 9.0) do not appear at the API boundary. They happen inside the system.

---

## The 8.0 exception

8.0 Manage Events and Programs is the only configuration-layer process that writes an operational record — an event-sourced ServiceOccurrence, created when a Program Item is linked to a Service Definition.

The endpoint that performs this action must reflect the side effect:

- The request is a configuration-layer action (linking a Program Item to a Service Definition).
- The response acknowledges that a ServiceOccurrence was created as a consequence.

This is the only case where a configuration-layer endpoint produces an operational record, and it must be documented as such when the API contract is defined.

---

## What the API must not do

These constraints come from the process model, not from any API design choice:

- **No composite endpoints that stitch together behavior from multiple processes.** One endpoint, one process or subprocess.
- **No writes outside the process's store footprint.** The endpoint inherits the process's footprint; it does not extend it.
- **No new invocation edges.** The endpoint invokes one process; it does not cause that process to invoke another, beyond the four locked edges.
- **No delivery status on notifications.** 9.0 is not exposed; the member-facing path's fire-and-forget behavior and the authority-notification sweep's at-least-once retry are both internal. Neither is observable through the API.
- **No fabricated state.** If a process refuses to run because a required setting is absent, the endpoint returns a configuration error, not a fabricated response.

---

## Cross-references

- **`endpoints.md`** — the eventual endpoint list.
- **`adapter-contract.md`** — the application-level contract between HTTP and the processes.
- **`request-response.md`** — the request/response conventions.
- **`../processes/`** — the processes the endpoints will expose.
- **`../architecture/invocation-model.md`** — the invocation model the API must respect.
- **Specification §3, §16** — the authoritative sources.

---

*Source: System Design Specification §3, §16. Anticipated exposure — not a finalized API design contract.*
