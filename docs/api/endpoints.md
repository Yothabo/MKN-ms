# Endpoints

*Placeholder for the endpoint list. To be populated once the API contract exists.*

---

## Status

Not yet defined. The endpoint list depends on the technology stack and the API style (REST, RPC, GraphQL, or other), which have not yet been chosen.

---

## What will live here

When the API contract is defined, this file will list every endpoint the system exposes. Each endpoint will be organised by the process it serves.

The anticipated groups, mirroring `../processes/`:

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
- Dispatch Notification (9.0) — internal, not exposed
- Evaluate Fill Status (10.0)
- Materialize Occurrences (11.0)

The Notification Dispatcher (9.0) is invoked internally by 5.0 and 7.0; it does not require an externally exposed endpoint.

---

## Constraints the endpoint list will respect

- **One endpoint, one process (or subprocess).** An endpoint invokes exactly one process or subprocess. No composite endpoints that stitch together behavior from multiple processes.
- **No invented behavior.** Endpoints cannot introduce writes outside the process's store footprint.
- **No new invocation edges.** Endpoints trigger processes; they do not cause processes to invoke each other beyond the two locked edges.
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

- **`process-mapping.md`** — the mapping from endpoints to processes.
- **`request-response.md`** — the request/response conventions.
- **`../processes/`** — the processes the endpoints expose.

---

*Source: System Design Specification §2, §16. Populated after stack selection.*
