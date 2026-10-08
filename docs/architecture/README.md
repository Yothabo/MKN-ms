# Architecture

*The system described top-down. Eight documents covering the design principle, the components, the entities, the processes, and the invocation model. Derived from the System Design Specification. Where a document in this directory conflicts with the specification, the specification wins.*

---

## Purpose

This directory holds the derived architecture documents. Each describes one aspect of the system as a whole. Each is derived from the specification and subordinate to it.

The specification is the authority. The documents here render it in implementation-oriented language without extending it.

---

## The documents

### `system-overview.md`

A top-down description of what MKN-MS is, what it is built from, and how its layers fit together. Start here for orientation.

### `core-design-principle.md`

The single rule that governs every other decision: fixed concepts versus administrator configuration. Every other document depends on this one.

### `fixed-vs-configurable.md`

What the system knows without being told, and what an administrator must decide. The principle from `core-design-principle.md`, applied concretely.

### `components.md`

The system's components and their responsibilities. The components table, and each component's boundary — what it does and what it does not do.

### `data-model.md`

The entities of the system and the relationships between them. A navigational view, with a store summary, a relationship diagram, and the entity groups.

### `entity-reference.md`

Every table in the schema, every column, its type, and its notes. The complete column-level reference. Complemented by `../database/schema.md`, which presents the same material for the implementation-facing reference.

### `process-model.md`

The thirteen processes that make up the system, grouped by layer. Each process's trigger, footprint, and invariants.

### `invocation-model.md`

How the system's processes communicate. The four direct invocation edges terminating at 9.0, the data-mediated relationships, and the invocation-versus-triggering distinction.

---

## Reading order

For a reader new to the system:

1. **`system-overview.md`** — the top-down view.
2. **`core-design-principle.md`** — the governing rule.
3. **`fixed-vs-configurable.md`** — the rule applied.
4. **`components.md`** — the components.
5. **`data-model.md`** — the entities and their relationships.
6. **`entity-reference.md`** — every column of every table.
7. **`process-model.md`** — the thirteen processes.
8. **`invocation-model.md`** — how they communicate.

---

## What this directory does not contain

- The specification itself. That is at `../spec/system-design-spec.md`.
- The process contracts. Those are at `../processes/`.
- The database reference. That is at `../database/`.
- The API adapter contract. That is at `../api/`.
- The named concerns whose content is deliberately open. Those are at `../modules/`.

---

*Source: System Design Specification; the eight documents in this directory.*
