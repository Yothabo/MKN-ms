# Specification

*Authoritative reference material. Every other directory in this documentation tree is derived from these documents. Where a derived document conflicts with a specification document, the specification wins.*

---

## Documents

### `system-design-spec.md`

The System Design Specification. Architecture-only — contains no configuration data from any specific congregation.

Authoritative for:

- What the system is capable of representing and executing.
- The core design principle (fixed categories vs. administrator configuration).
- The entity definitions (§4).
- The configuration language and criteria vocabulary (§5).
- What remains fixed and why (§6).
- The resolved occurrence materialization design (§7).
- The derived consequences of every locked contract (§8–§17).
- The consolidated schema amendments (§15).
- The process invocation topology (§16).

This is the single source of truth for the system's architecture. Everything in `architecture/`, `processes/`, `database/`, and `api/` derives from it.

### `dfd.md`

The Data Flow Diagrams. Scoped strictly to the current System Design Specification. Standard DFD notation — rectangles for external entities, circles for processes, cylinders for data stores.

Authoritative for:

- The context diagram (Level 0).
- The process decomposition (Level 1).
- The Level 2 decompositions for processes 1.0, 3.0, 5.0, and 11.0.
- The process dictionary and data store dictionary.
- The visual representation of data flows between processes and stores.

The DFD is consistent with the System Design Specification but presents the same architecture from a data-flow perspective rather than a contracts perspective.

### `config-reference.md`

The MKN Configuration Reference. Contains configuration data specific to MKN — the roles, duties, tiers, and other values an administrator would enter into the system's configuration layer.

Not architecture. What MKN has configured, not what the system is capable of representing. The rule-neutral architecture that these values are entered into is defined in `system-design-spec.md`.

Authoritative for:

- The specific configuration values MKN uses.
- The scope of MKN's current deployment (which services exist, which branches, which roles).
- The defaults marked "confirm with MKN" and their reasoning.

---

## Reading Order

For a reader new to the system:

1. `system-design-spec.md` §1–§3 — purpose, design principle, components.
2. `system-design-spec.md` §4 — the entities.
3. `system-design-spec.md` §5–§6 — the configuration language and what is fixed.
4. `system-design-spec.md` §7 — the materialization contract.
5. `dfd.md` — the same architecture, viewed as data flows.
6. `system-design-spec.md` §8–§16 — the derived contracts and consolidations.
7. `config-reference.md` — how MKN has actually configured the system.

Then move outward into `../architecture/`, `../processes/`, `../database/`, and `../api/`.

---

## Authority

These documents are the authority. The derived documentation in the sibling directories is subordinate to them. Any conflict between a derived document and these documents is resolved in favor of these documents, and the derived document is corrected.

Any change to the system's architecture begins here, not in the derived documentation.
