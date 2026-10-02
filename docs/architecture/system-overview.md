
# System Overview

*A top-down description of what MKN-MS is, what it is built from, and how its layers fit together. Derived from the System Design Specification §1–§3 and §6. Where this document conflicts with the specification, the specification wins.*

---

## What the system is

MKN-MS is a configurable rostering and membership platform. It represents and executes the mechanics of recurring service rosters, member registers, duty assignments, confirmations, attendance, and events — but it knows nothing about any specific organization's rules, roles, duties, or priorities until an administrator configures them.

The system is built to answer a single kind of question: *given whatever an organization has configured, what should happen next, mechanically?* It does not answer questions about what an organization *should* configure.

---

## The two kinds of decisions

Everything in the system separates into two categories that are never allowed to collapse into each other:

**Categories** — the fixed concepts the system is built from. Member, Role, Duty, Service, Eligibility, Duty Rule, Assignment, Attendance, Confirmation, Admin, Event, Program. These exist because a rostering system needs a vocabulary to represent anything at all. They carry no organizational meaning by themselves.

**Configuration** — every relationship, threshold, priority, permission, and outcome definition. Entered entirely by an administrator, stored as data, never hardcoded.

An unconfigured category is empty and inert. A Duty with no Duty Rules attached requires nothing and permits nothing until an administrator says otherwise.

---

## The five-layer chain

The architecture, end to end, is a chain of five distinct layers, and no layer is allowed to collapse into another:

```

System vocabulary → stored facts → admin configuration → engine interpretation → operational outcomes

```

- **System vocabulary** — the entity definitions. Fixed.
- **Stored facts** — Member records, identifier history, and other data the administrator enters directly.
- **Admin configuration** — the relationships and rules the administrator expresses using the vocabulary. Duty Rules, Eligibility grants, Service Definitions, Schedules.
- **Engine interpretation** — the operational processes reading configuration and stored facts at run time.
- **Operational outcomes** — occurrences materialized, assignments generated, attendance recorded, fill status evaluated.

The engine never contains any organization's operating rules. It only knows how to execute whatever configuration exists.

---

## The four categories

When reasoning about any part of the system, four distinct things are worth holding separately:

| Category | What it is |
| --- | --- |
| **Fixed** | What the system can represent, and what the engine knows how to mechanically calculate |
| **Configurable** | What an administrator chooses to express using that capability |
| **Operational data** | What has actually happened — Member records, Attendance, Roster Assignments, materialized Occurrences |
| **MKN configuration** | The particular values administrators eventually enter. Documented separately, never in the specification |

---

## The components

| Component | Responsibility |
| --- | --- |
| Rules (configuration layer) | Stores every administrator-entered value: role/duty relationships, thresholds, tiers, permissions, outcome states |
| Eligibility | Evaluates a binary constraint for a member against a duty, based on whatever the administrator has configured |
| Priority | Orders eligible candidates according to whatever tier structure the administrator has configured for that duty |
| Assignment | Orchestrates filling a service occurrence's required duties, using Eligibility and Priority; never resolves a gap on its own initiative |
| Fill Status | Evaluates the outcome of an occurrence against administrator-defined states and triggers |
| Confirmation | Tracks each assignment's response lifecycle; re-resolves only the affected slot on a decline or timeout |
| Attendance | Records the raw fact of presence at an occurrence; no interpretation layer built in |
| Identity/Card | Manages identifier issuance and reassignment history for a member |
| Occurrence Materializer | Generates Service Occurrence records from active Service Schedules on a rolling time horizon; additive and idempotent — never modifies or deletes an existing occurrence |
| Notification Dispatcher | Sends messages through whichever channel is configured |
| Program | Manages an ordered schedule of items belonging to an Event, independent of roster/duty logic; item state is computed, not configured |

---

## What remains fixed, and why

The entity categories and the criteria-type vocabulary are the fixed system vocabulary. Other fixed behavior exists only where the system must perform a defined mechanical operation on stored facts — deriving a Program Item's lifecycle state from scheduled times, or materializing Service Occurrences from active Service Schedules on a rolling horizon.

Neither of these is a category, and neither is an administrator setting. Both are computed or generated as a matter of correct operation. No organization-specific operating rule is ever fixed by the system.

---

## The required-setting rule

One further rule governs every process in the system:

**Required configuration that a process depends on must fail loudly when absent, never idle silently.**

An unconfigured category is inert and harmless. A process that depends on a specific setting to run correctly and finds that setting missing must stop and surface that fact, not proceed as if nothing were wrong.

This is why processes like the Occurrence Materializer refuse to run when `OccurrenceHorizonDays` is unset, and why every process that consumes a required setting behaves the same way.

---

## What the system does not do

- It does not assume any organization's operating rules.
- It does not pre-link categories to each other. Role and Duty have no relationship until an administrator creates one via a Duty Rule.
- It does not interpret configuration on its own initiative. Every interpretation is a defined mechanical operation performed by a specific process.
- It does not manage security, sessions, authorization, retention, or support infrastructure. Those are outside the current specification.

---

## Where to read next

- **`core-design-principle.md`** — the fixed/configurable separation, in full.
- **`components.md`** — the components table with implementation-facing notes.
- **`fixed-vs-configurable.md`** — what the system knows vs. what administrators decide.
- **`data-model.md`** — the entities and their relationships.
- **`entity-reference.md`** — every table, every column.
- **`process-model.md`** — the eleven processes.
- **`invocation-model.md`** — how processes communicate.

---

*Source: System Design Specification §1, §2, §3, §6.*
