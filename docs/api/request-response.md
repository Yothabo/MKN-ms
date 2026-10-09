# Request / Response Conventions

*Placeholder for request and response shape conventions. To be populated once the API contract exists.*

---

## Status

The technology stack is chosen: ASP.NET Core Web API, JSON request and response bodies.

The authoritative conventions are in `adapter-contract.md`. This file states the derived conventions that follow from it and from `../processes/contracts.md`: how request bodies are shaped, how responses are shaped, how errors are surfaced, and how the caller distinguishes an operation failure from a notification failure.

---

## What will live here

When the API contract is defined, this file will document:

- **Request shape conventions.** How request bodies are structured, how parameters are passed, how bulk operations are handled.
- **Response shape conventions.** Success responses, error responses, pagination, partial success, and identifiers.
- **Error conventions.** Error codes, error payload shape, and the mapping between the system's lock-error behaviors (required-setting failure, constraint violation, etc.) and the HTTP or protocol responses.
- **Idempotency conventions.** How idempotent endpoints behave on repeat calls.
- **Concurrency conventions.** How conflicts are surfaced and resolved.
- **Versioning.** Whether the API is versioned, and how.

---

## Constraints the conventions will respect

- **Required-setting errors are configuration errors.** When a process refuses to run because a required setting is absent, the endpoint returns a configuration-error response, not a generic failure.
- **Constraint violations are surfaced, not swallowed.** A duplicate insert against a uniqueness constraint returns an explicit error.
- **The 8.0 exception is documented.** The endpoint that links a Program Item to a Service Definition produces an event-sourced occurrence; its request and response shapes reflect that side effect.
- **No delivery tracking for notifications.** 9.0 is fire-and-forget; its endpoints (if exposed) do not return delivery status.
- **Fill status and assignment status are distinct concepts.** Their errors and responses do not conflate.

---

## Format (proposed)

The conventions will be documented as prose plus example payloads, once the API style is fixed. The exact format will be chosen then.

---

## Cross-references

- **`endpoints.md`** — the endpoint list.
- **`process-mapping.md`** — the mapping to processes.
- **`../database/settings.md`** — the settings whose absence produces configuration errors.
- **`../database/constraints.md`** — the constraints whose violation produces constraint errors.

---

*Source: System Design Specification §2, §15. Populated after stack selection.*
