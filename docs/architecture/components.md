# Components

*The system's components and their responsibilities. Derived from the System Design Specification §3. Where this document conflicts with the specification, the specification wins.*

---

## The components

The system is composed of eleven named components. Each has a defined responsibility, and each respects the core design principle — none of them embeds an organization's rules.

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

## How components map to processes

Each component is implemented by one or more processes in the DFD. The mapping is direct:

| Component | Process(es) |
| --- | --- |
| Rules (configuration layer) | 1.0, 2.0, 3.0, 4.0, 8.0 |
| Eligibility | 4.0 (write), 5.0 (read via 5.1) |
| Priority | 5.0 (read via 5.2) |
| Assignment | 5.0 |
| Fill Status | 10.0 |
| Confirmation | 7.0 |
| Attendance | 6.0 |
| Identity/Card | 3.0 (subprocess 3.2) |
| Occurrence Materializer | 11.0 |
| Notification Dispatcher | 9.0 |
| Program | 8.0 |

A component may be implemented by more than one process when the component spans configuration and operations. Rules, for example, is written to by five configuration processes and read from by most operational processes.

---

## Component boundaries

Each component has a defined boundary — what it does and does not do. These boundaries are load-bearing; they are what allow the system's processes to remain independently executable.

### Rules (configuration layer)

- **Does:** store every administrator-entered value as data.
- **Does not:** evaluate any rule, threshold, or criterion. Evaluation belongs to operational components.
- **Does not:** link categories to each other. A Duty Rule row expresses a relationship; it does not create a hard link between Role and Duty.

### Eligibility

- **Does:** evaluate a binary constraint for a member against a duty.
- **Does not:** rank candidates. Ranking is Priority's responsibility.
- **Does not:** determine fill state. That is Fill Status.

### Priority

- **Does:** order eligible candidates according to configured tier structure.
- **Does not:** determine candidacy. A member who is not eligible never enters Priority's candidate set.
- **Does not:** choose among candidates when a tier produces more than the required slot count. That is Assignment's concern, using whatever deterministic order Assignment applies.

### Assignment

- **Does:** orchestrate filling a service occurrence's required duties, using Eligibility and Priority.
- **Does not:** resolve gaps on its own initiative. If no candidate is available for a duty, the slot remains unfilled.
- **Does not:** modify or remove existing assignments. It is additive.

### Fill Status

- **Does:** evaluate the outcome of an occurrence against configured states.
- **Does not:** assign or modify assignments. It reads them.
- **Does not:** trigger downstream processes. It writes `FillStatusID` and stops.

### Confirmation

- **Does:** track each assignment's response lifecycle.
- **Does:** re-resolve only the affected slot on a decline or timeout.
- **Does not:** create the initial assignment. That is Assignment's responsibility.
- **Does not:** modify the declined or timed-out row beyond the status transition.

### Attendance

- **Does:** record the raw fact of presence at an occurrence.
- **Does not:** interpret attendance. No recency computation, no streaks, no derived values.
- **Does not:** condition attendance on assignment. A member may attend without being rostered, and vice versa.

### Identity/Card

- **Does:** manage identifier issuance and reassignment history for a member.
- **Does not:** interpret what an identifier type means. The type is administrator-defined; the system manages the assignment/history lifecycle.

### Occurrence Materializer

- **Does:** generate Service Occurrence records from active Service Schedules on a rolling time horizon.
- **Does:** run idempotently — repeated runs produce no new rows once coverage is complete.
- **Does not:** modify or delete an existing occurrence, ever.
- **Does not:** invoke any other process.

### Notification Dispatcher

- **Does:** send messages through whichever channel is configured.
- **Does not:** track delivery, retry, or log.
- **Does not:** invoke any other process.
- **Is invoked only by:** Assignment (5.0) and Confirmation (7.0), on new assignment creation.

### Program

- **Does:** manage an ordered schedule of items belonging to an Event.
- **Does not:** participate in roster/duty logic.
- **Does:** compute item state (upcoming/current/done) from scheduled times; item state is not configured.

---

## What the components have in common

Every component in the system:

- Reads its configuration at run time; none has it embedded.
- Respects the required-setting rule — if it depends on a setting that is absent, it fails loudly rather than proceeding.
- Writes only to the stores it is contractually allowed to write to; it never modifies a store outside its footprint.
- Does not orchestrate another component, except for the two locked invocation edges (5.0 → 9.0 and 7.0 → 9.0).

---

*Source: System Design Specification §3.*
