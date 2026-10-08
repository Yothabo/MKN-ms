# Modules

*A documentation grouping for named system concerns whose content is deliberately open. Not an architectural claim. Where a document in this directory conflicts with the specification, the specification wins.*

---

## Purpose

This directory names five system concerns that the specification recognises but has not yet made implementation-ready. Each has its own boundary stated, its current status stated, and its non-dependency on the defined processes stated.

The directory is a grouping. It does not assert that these concerns are modules in the architectural or process sense. They may become processes, entities, settings, or something else when their content is decided. Until then, they are named concerns with open content.

## The five concerns

- `authentication.md`
- `authorization.md`
- `notification-templates.md`
- `per-member-channel-preferences.md`
- `tap-ingestion.md`

## What this directory does not contain

It does not contain fields, entities, process contracts, APIs, storage mechanisms, invocation edges, or implementation assumptions. Each file states only what is named above.

## Relationship to the specification

The specification is authoritative. Each concern is recognised by the specification and marked as open there. The files in this directory restate the boundary and the status. They do not extend either.

The thirteen defined processes do not depend on the content of any concern in this directory. Every process defined by the specification can be implemented without those contents being decided.

---

*Source: System Design Specification; FREEZE.md.*
