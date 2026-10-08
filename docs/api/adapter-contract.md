# Adapter Contract

*The contract between the HTTP surface and the process contracts. Derived from the process contracts and the specification. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states the shape of the HTTP adapter that sits between an external caller and the processes. It does not fix the HTTP style. It fixes what any HTTP style must carry, and how the process contracts map to HTTP.

The API directory currently contains placeholders describing what will live here once the stack is chosen. This document is the first content: it fixes the application-level contract; it leaves the transport style open.

---

## The division of responsibility

Three layers.

**The HTTP adapter** parses the request, validates its shape, invokes the process, and translates the process result to an HTTP response. It performs no business logic.

**The process** implements the contract in `docs/processes/contracts.md`. It receives a command, applies its rules, and returns a result or throws.

**The persistence layer** executes the writes the process asks for, subject to the transaction boundaries stated in §9.8.

The adapter is the outer shell. It exists to translate HTTP into the process contract and back. Every business rule lives in the process.

---

## The endpoint inventory

Each process has an HTTP surface. The current endpoint files at the freeze commit carry one endpoint per process, with the configuration layer grouped into a single file.

| Process | Endpoint file at freeze | Endpoint count |
| --- | --- | --- |
| 1.0, 2.0, 3.0, 4.0, 8.0 | ConfigurationEndpoints.cs | 54 route mappings |
| 5.0 | GenerateAssignmentEndpoint.cs | 1 |
| 6.0 | RecordAttendanceEndpoint.cs | 1 |
| 7.0 | ManageConfirmationEndpoint.cs | 2 |
| 10.0 | EvaluateFillStatusEndpoint.cs | 1 |
| 11.0 | MaterializeOccurrencesEndpoint.cs | 1 |
| 12.0 | CreateManualAssignmentEndpoint.cs | 1 |
| 9.0, 13.0 | no HTTP surface at freeze | 0 |

Two processes have no HTTP surface:

- **9.0 Dispatch Notification** is invoked internally by 5.0, 7.0, 12.0, and 13.0 on Path 1, and by the scheduler on Path 2. It has no external caller.
- **13.0 Attendance Rule Engine** runs on a schedule on its scheduled path. Its manual invocation does not have an HTTP surface at the freeze commit.

Adding an HTTP surface for 13.0's manual invocation is a later addition. The endpoint file would be AttendanceRuleEngineEndpoint.cs.

---

## The request shape

Each endpoint receives an HTTP request. The request carries:

- The command fields stated in `docs/processes/contracts.md` for the process.
- The identity of the acting admin, when the process receives one.

The identity is not carried in the request body. It is carried by whatever mechanism the HTTP layer uses for caller identity — a header, a session, a token. The specification does not fix the mechanism. What the specification fixes is that the process receives an AdminID, and that the adapter resolves the caller to that AdminID.

At the freeze commit, the adapter accepts the admin ID as a plain field in the request body. This is a development convenience. When an authentication mechanism is introduced, the adapter resolves the ID from the authenticated caller and ignores any body value. The process contract does not change; only the adapter's source for the ID.

### Request field validation

The adapter validates the request's shape before invoking the process. Required fields are checked for presence. Field types are checked. Dates and times are parsed. Enumerated values are checked against their vocabulary.

A shape failure produces a ValidationFailed response and the process is not invoked.

### Field naming

Field names in the request body match the command field names stated in `docs/processes/contracts.md`, using camelCase. OccurrenceId becomes occurrenceId. ActingAdminId becomes actingAdminId. FromDate becomes fromDate.

The adapter is responsible for the mapping. The process receives the contract field name regardless of how the adapter names it externally.

---

## The response shape

Every response is JSON. Every response carries at least a status field whose value is one of two strings:

    {
      "status": "Success"
    }

or

    {
      "status": "Failure"
    }

### Success responses

A success response carries the status field set to Success, plus the process-specific fields named in `docs/processes/contracts.md`.

For a create operation, the response carries the created entity's primary key. For a batch operation, it carries the counts.

### Failure responses

A failure response carries the status field set to Failure, an errorCode field naming one of the error categories in `docs/database/errors.md`, and an errorDetail field describing the failure in administrator-readable text. For example:

    {
      "status": "Failure",
      "errorCode": "validation_failed",
      "errorDetail": "AssignmentId is required and must be positive."
    }

### Thrown exceptions

A process may throw. A thrown exception means a system fault, not a caller error. The adapter's error handler catches the throw and returns an HTTP status corresponding to the exception type. The specification does not require the thrown exception's content to be exposed to the caller. A 500-class response is appropriate.

---

## Error category to HTTP status mapping

The mapping below is a recommendation. The specification fixes the error categories; the HTTP status each maps to is an adapter choice. The mapping is stated so the adapter's behaviour is consistent across endpoints.

| Error category | HTTP status | Notes |
| --- | --- | --- |
| ValidationFailed | 400 Bad Request | The request is malformed or the fields violate their stated rules |
| NotFound | 404 Not Found | A referenced entity does not exist |
| Conflict | 409 Conflict | A uniqueness or state constraint is violated |
| LifecycleViolation | 422 Unprocessable Entity | The entity exists but the requested transition is not permitted |
| ConfigurationMissing | 422 Unprocessable Entity | A required setting is absent |
| ConfigurationInvalid | 422 Unprocessable Entity | A required setting is present but invalid |
| ConcurrencyConflict | 409 Conflict | A concurrent write prevented an invariant from holding |
| DependencyFailure | 503 Service Unavailable | An external dependency could not be reached |
| InvariantViolation | 500 Internal Server Error | A should-be-impossible condition occurred |

The distinction between 400 and 422: 400 is a request that the adapter refuses before invoking the process; 422 is a request the adapter passes to the process, and the process refuses it. The line between them is the shape validation boundary described in C4.

---

## What the adapter does not do

- **It does not authorise.** Authentication and authorization are upstream of the adapter. The adapter passes the resolved AdminID to the process; whether the admin is permitted to perform the operation is decided before the adapter is reached.
- **It does not validate against the database.** The adapter does not query the database. It validates the request's shape. Referenced entity existence is the process's concern.
- **It does not perform business logic.** Every rule the system applies lives in a process. The adapter's job is translation.
- **It does not catch every exception.** A process that throws an InvariantViolation has found a defect. The adapter's error handler surfaces it; the adapter does not attempt recovery.

---

## Scheduled processes

Four processes have scheduled triggers: 11.0, 5.0, 7.0, and 10.0. Two more, 9.0 and 13.0, run on scheduled triggers as well. None of them require an HTTP endpoint to be triggered on schedule. The scheduler invokes them directly.

Each scheduled process also has a manual administrative trigger, except 9.0's authority sweep and 10.0's sweep, which are scheduled only. The manual triggers are HTTP endpoints:

- 5.0, 11.0, 7.0's respond, 10.0's range evaluation, 12.0, and 6.0 have endpoints at the freeze commit.
- 13.0's manual rule invocation has no endpoint at the freeze commit. Adding one is a later addition.

---

## Swagger and OpenAPI

At the freeze commit, the API exposes Swagger at /swagger in Development. The OpenAPI document is generated by Swashbuckle from the endpoint metadata.

The OpenAPI document is a byproduct of the adapter's implementation. It is not authoritative. If it disagrees with `docs/processes/contracts.md`, the contracts document wins.

---

## What the adapter leaves open

- **The HTTP style.** REST, RPC, or other. The endpoint inventory names process groupings, not URL shapes. The freeze commit uses /api/processes/{process}/... and /api/config/{process}/... as URL conventions. A future revision may change them.
- **Authentication mechanism.** Not part of the freeze.
- **Authorisation mechanism.** Not part of the freeze.
- **Versioning.** Not part of the freeze.
- **Pagination for list endpoints.** Not part of the freeze.
- **Content negotiation.** JSON at the freeze; other formats are a later choice.
- **Rate limiting.** Not part of the freeze.

---

*Source: System Design Specification §16; `docs/processes/contracts.md`; `docs/database/errors.md`; the freeze commit's endpoint inventory.*
