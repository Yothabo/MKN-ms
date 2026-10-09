# API

*Placeholder for the system's API documentation. To be populated once the API contract exists.*

---

## Status

The technology stack is chosen: ASP.NET Core Web API with Minimal API endpoints, on .NET 8, backed by EF Core 8 and PostgreSQL.

`adapter-contract.md` is the authoritative application-level contract between the HTTP surface and the processes. It states the request shape, the response shape, the error-category-to-HTTP mapping, and what the adapter does and does not do.

`endpoints.md`, `request-response.md`, and `process-mapping.md` are derived from that contract and from `../processes/contracts.md`. They are being brought into line with it. Where any of them conflicts with `adapter-contract.md`, the adapter contract wins; where `adapter-contract.md` conflicts with the specification, the specification wins.

---

## What will live here

When the API contract is defined, this directory will hold:

- **`endpoints.md`** — the endpoint list, grouped by the processes they serve.
- **`request-response.md`** — request and response shapes, error conventions, and any versioning policy.
- **`process-mapping.md`** — the anticipated process exposure, mapped against the thirteen processes in `../processes/`.

---

## What governs the API

The API is a presentation of the processes defined in `../processes/`, not an independent layer. Its design is constrained by:

- **The process boundaries.** Each endpoint invokes exactly one process or subprocess. No composite endpoints that stitch together behavior from multiple processes.
- **The store footprints.** An endpoint must not write to a store outside the footprint of the process it invokes.
- **The invocation model.** Endpoints trigger processes; they do not introduce new invocation edges. The four direct edges (5.0 → 9.0, 7.0 → 9.0, 12.0 → 9.0, and 13.0 → 9.0) are internal and do not appear at the API boundary.
- **The required-setting rule.** Endpoints that trigger processes depending on a required setting must surface the configuration error if the setting is absent.
- **The 8.0 exception.** An endpoint that links a Program Item to a Service Definition triggers the creation of an event-sourced occurrence. This is the only case where a configuration-layer action produces an operational record.

---

## When this directory will be populated

After the technology stack is chosen and the first process is implemented. The first content will likely be `process-mapping.md` — a table listing each process, its trigger, and the endpoint (if any) that invokes it.

Until then, the files in this directory stay as placeholders.

---

## Cross-references

- **`../processes/`** — the processes the API will expose.
- **`../architecture/invocation-model.md`** — the invocation model the API must respect.
- **`../architecture/core-design-principle.md`** — the core principle the API must not violate.
- **Specification §2, §3, §16** — the authoritative sources.

---

*Source: System Design Specification §2, §16. Populated after stack selection.*
