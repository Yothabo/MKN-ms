# Process Mapping

*How API endpoints will map to the system's processes. Derived from the System Design Specification §3 and §16. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document defines the mapping from the eventual API endpoints to the system's eleven processes. It is the design contract the API must respect: every endpoint invokes a process or subprocess, and no endpoint introduces behavior beyond what the process defines.

The endpoint list itself is not yet defined. This document establishes the shape of the mapping.

---

## The mapping

Each process is exposed (or not exposed) through the API according to its trigger and its relationship to external actors.

| # | Process | Trigger | Exposed as endpoint? |
| --- | --- | --- | --- |
| 1.0 | Configure Vocabulary | Administrator action | Yes — admin interface |
| 2.0 | Configure Duty Rules | Administrator action | Yes — admin interface |
| 3.0 | Manage Membership | Administrator action | Yes — admin interface |
| 4.0 | Manage Eligibility | Administrator action | Yes — admin interface |
| 5.0 | Generate Assignment | Scheduled; manual admin | Yes — admin trigger; also runs on schedule |
| 6.0 | Record Attendance | Member action; manual admin | Yes — member-facing; also admin entry |
| 7.0 | Manage Confirmation | Member response; scheduled | Yes — member-facing response; timeout on schedule |
| 8.0 | Manage Events and Programs | Administrator action | Yes — admin interface |
| 9.0 | Dispatch Notification | Invoked by 5.0 / 7.0 | No — internal; not exposed |
| 10.0 | Evaluate Fill Status | After assignment changes; scheduled | No — runs internally |
| 11.0 | Materialize Occurrences | Scheduled; manual admin | Yes — admin trigger; also runs on schedule |

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
| 11.1–11.7 Materialization steps | 11.0 |

The 5.1–5.3 and 11.1–11.7 subprocesses are internal steps of their parent processes. They are not exposed as separate endpoints — the parent process runs them as a single invocation.

---

## The trigger mapping

Each trigger maps to a specific exposure style:

| Trigger | Exposure |
| --- | --- |
| Administrator action | Admin-facing endpoint |
| Member action | Member-facing endpoint |
| Scheduled execution | Not exposed; runs in the scheduler |
| Manual admin trigger of a scheduled process | Admin-facing endpoint that runs the process on demand |
| Invocation by another process | Not exposed; internal |
| After-assignment-change trigger | Not exposed; runs internally |

The two internal invocation edges (5.0 → 9.0 and 7.0 → 9.0) do not appear at the API boundary. They happen inside the system.

---

## The 8.0 exception

8.0 Manage Events and Programs is the only configuration-layer process that writes an operational record — an event-sourced ServiceOccurrence, created when a Program Item is linked to a Service Definition.

The endpoint that performs this action must reflect the side effect:

- The request is a configuration-layer action (linking a Program Item to a Service Definition).
- The response acknowledges that a ServiceOccurrence was created as a consequence.

This is the only case where a configuration-layer endpoint produces an operational record, and it must be documented as such.

---

## What the API must not do

- **No composite endpoints that stitch together behavior from multiple processes.** One endpoint, one process (or one subprocess).
- **No writes outside the process's store footprint.** The endpoint inherits the process's footprint; it does not extend it.
- **No new invocation edges.** The endpoint invokes one process; it does not cause that process to invoke another, beyond the two locked edges.
- **No delivery status on notifications.** 9.0 is not exposed; its fire-and-forget behavior is not observable through the API.
- **No fabricated state.** If a process refuses to run because a required setting is absent, the endpoint returns a configuration error, not a fabricated response.

---

## Cross-references

- **`endpoints.md`** — the eventual endpoint list.
- **`request-response.md`** — the request/response conventions.
- **`../processes/`** — the processes the endpoints expose.
- **`../architecture/invocation-model.md`** — the invocation model the API respects.
- **Specification §3, §16** — the authoritative sources.

---

*Source: System Design Specification §3, §16.*
