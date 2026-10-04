
# Documentation

*The entry point for this documentation tree. Everything under `docs/` is either the specification — the authority — or a derived document. Where a derived document conflicts with the specification, the specification wins.*

---

## How this tree is organized

```

docs/
├── README.md              
├── spec/                  ← the authority
├── architecture/          ← derived: the system as a whole
├── processes/             ← derived: process-by-process
├── database/              ← derived: implementation-facing data model
└── api/                   ← derived: placeholder until stack is chosen

```

Each directory has a distinct role:

| Directory | Role |
| --- | --- |
| `spec/` | The authoritative specification documents. Nothing here derives from anything else. |
| `architecture/` | The system described top-down. Derived from the spec. |
| `processes/` | One document per process. Derived from the spec. |
| `database/` | The implementation-facing reference for the data model. Derived from the spec. |
| `api/` | A placeholder that will be populated once the stack is chosen. |

---

## Reading order

For a reader new to the system:

1. **`spec/system-design-spec.md` §1–§3** — purpose, design principle, components.
2. **`spec/system-design-spec.md` §4** — the entities.
3. **`spec/system-design-spec.md` §5–§6** — the configuration language and what is fixed.
4. **`spec/system-design-spec.md` §7** — the materialization contract.
5. **`spec/dfd.md`** — the same architecture, viewed as data flows.
6. **`spec/system-design-spec.md` §8–§16** — the derived contracts and consolidations.
7. **`spec/config-reference.md`** — how MKN has actually configured the system.

Then move outward into the derived documentation:

8. **`architecture/system-overview.md`** — the top-down view.
9. **`architecture/core-design-principle.md`** — the fixed/configurable separation, in full.
10. **`architecture/data-model.md`** and **`entity-reference.md`** — the entities.
11. **`architecture/process-model.md`** — the eleven processes.
12. **`architecture/invocation-model.md`** — the two invocation edges and the data-mediated relationships.
13. **`processes/configuration/README.md`** and **`processes/operations/README.md`** — the two process layers.
14. **The individual process documents** — one per process.
15. **`database/`** — the implementation-facing reference.
16. **`api/`** — the eventual API surface.

---

## The authority

The spec is the authority. Everything else in this tree is derived from it.

The governing rule for every derived document is:

1. State the rule clearly.
2. Explain it in implementation-oriented language.
3. Reference the authoritative spec section.
4. Do not silently introduce anything not present in the spec.
5. Do not duplicate large sections of the specification.
6. If the derived document conflicts with the spec, the spec wins.

Any change to the system's architecture begins in `spec/`, not in a derived document.

---

## The directories in detail

### `spec/`

The three authoritative documents:

- **`system-design-spec.md`** — the System Design Specification. §1 through §16. Architecture-only.
- **`dfd.md`** — the Data Flow Diagrams. The same architecture from a data-flow perspective.
- **`config-reference.md`** — the MKN Configuration Reference. What MKN has actually configured. Not architecture.

See **`spec/README.md`** for the full description of each.

### `architecture/`

Eight documents describing the system as a whole:

- **`system-overview.md`** — what the system is, top-down.
- **`core-design-principle.md`** — the fixed/configurable separation, in full.
- **`components.md`** — the components table and their boundaries.
- **`fixed-vs-configurable.md`** — what the system knows vs. what administrators decide.
- **`data-model.md`** — the entities and their relationships.
- **`entity-reference.md`** — every table, every column.
- **`process-model.md`** — the eleven processes, grouped by layer.
- **`invocation-model.md`** — the two invocation edges and the data-mediated relationships.

### `processes/`

The eleven processes, split into two layers:

- **`configuration/`** — the five processes that create and maintain administrator-entered values.
- **`operations/`** — the six processes that produce operational records.

Each subdirectory has a README describing the layer, plus one document per process.

### `database/`

The implementation-facing reference for the data model:

- **`schema.md`** — every table, column, type, nullability.
- **`constraints.md`** — every constraint, sourced.
- **`settings.md`** — every SystemSetting key.
- **`indexes.md`** — required, candidate, and physical indexes distinguished.
- **`amendments.md`** — the consolidated delta from the base schema.

### `api/`

A placeholder. Will be populated once the technology stack is chosen. Contains:

- **`endpoints.md`** — the eventual endpoint list.
- **`request-response.md`** — request/response conventions.
- **`process-mapping.md`** — how endpoints map to processes.

---

## The amendments at a glance

The locked operational contracts imply fifteen amendments to the base schema:

- **Two new columns:** RosterAssignment.CreatedAt, AssignmentStatus.IsTerminal.
- **Four new constraints:** RosterAssignment (MemberID, DutyID, OccurrenceID) unique; AttendanceRecord (MemberID, OccurrenceID) unique; ServiceSchedule (ServiceDefID, TimeSlotID) active-row unique; ServiceOccurrence (ScheduleID, Date) schedule-sourced unique.
- **Eight new settings:** OccurrenceHorizonDays, InitialAssignmentStatusID, ConfirmationTimeoutHours, OutcomeStateUnfilledID, OutcomeStatePartiallyFilledID, OutcomeStateFilledID, OutcomeStateCancelledID, NotificationChannel.
- **One nullability clarification:** RosterAssignment.AssignmentStatusID is nullable.

Full details in **`database/amendments.md`** and **`spec/system-design-spec.md` §15**.

---

## The invocation model at a glance

The system has three process-to-process invocation edges, all terminating at 9.0 Dispatch Notification:

- **5.0 Generate Assignment → 9.0 Dispatch Notification**, on each new automatic assignment.
- **7.0 Manage Confirmation → 9.0 Dispatch Notification**, on each replacement assignment.
- **12.0 Create Manual Assignment → 9.0 Dispatch Notification**, only when the created manual assignment has `AssignmentStatusID = NULL` at creation.

Everything else is data-mediated. No other process invokes or is invoked by anything.

Full details in **`architecture/invocation-model.md`** and **`spec/system-design-spec.md` §16**.

---

## The 8.0 exception

8.0 Manage Events and Programs is the sole configuration process permitted to create an operational record. When a Program Item is linked to a Service Definition, 8.0 creates an event-sourced ServiceOccurrence directly. This is the only write from the configuration layer to an operational store.

Full details in **`processes/configuration/8.0-manage-events-and-programs.md`** and **`spec/system-design-spec.md` §9**.

---

*Source: this file is a navigational index. It derives from nothing and introduces nothing. The authority is `spec/`.*
