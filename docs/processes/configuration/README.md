# Configuration Layer

*The five processes that create and maintain every administrator-entered value in the system. Derived from the System Design Specification §9. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

The configuration layer is the set of processes through which an administrator enters every organizational choice the system is capable of expressing. Its outputs are inert data — rows in configuration stores — that operational processes read.

Configuration processes do not read operational stores and do not invoke operational processes. They write only to configuration stores, with a single explicit exception: **8.0 Manage Events and Programs is the sole configuration process permitted to create an operational record**, and only because a Program Item has been explicitly linked to a Service Definition.

---

## The processes

| # | Process | Responsibility |
| --- | --- | --- |
| 1.0 | Configure Vocabulary | Roles, Duties, Branches, Time Slots, Service Definitions, Service Definition Duties, Service Schedules, and D10 lookups |
| 2.0 | Configure Duty Rules | Duty Rule rows expressing tiered criteria for ranking duty candidates |
| 3.0 | Manage Membership | Member records and Identifier History |
| 4.0 | Manage Eligibility | Eligibility grant/revoke records |
| 8.0 | Manage Events and Programs | Events, Programs, Program Items, Event Duties; event-sourced occurrences when a Program Item links to a Service Definition |

---

## The layer boundary

**Reads:** configuration stores only.

**Writes:** configuration stores only, plus the single 8.0 exception above.

**Does not touch:** `D7 Roster Assignment`, `D8 Attendance Record`, `D12 Materializer Run`, and any operational process.

**Does not invoke:** 5.0, 6.0, 7.0, 9.0, 10.0, or 11.0.

---

## Universal lifecycle semantics

Configuration entities whose contracts define lifecycle state use immediate activation and deactivation rather than deletion. Lifecycle semantics are defined per entity in the sections that follow. Entities whose contracts do not define lifecycle state — `IdentifierHistory`, `Eligibility`, `SystemSetting`, and `D10` lookup values — are governed by their own specific rules.

For entities that do carry lifecycle state, the rules are uniform:

- **Creation is immediate.** An entity is active from the moment it is created. No draft, no publish step, no approval gate.
- **Deactivation removes the entity from future use** without altering any record that references it.
- **Every field is editable, always.** Edits take effect for future use. No backward propagation to operational records.
- **No deletion, ever.** Configuration entities are historical facts about what the organization configured at a given time.

Entities with lifecycle state: `Role`, `Duty`, `Branch`, `BranchTimeSlot`, `ServiceDefinition`, `ServiceDefinitionDuty`, `ServiceSchedule`, `Member`, `DutyRule`, `Event`, `Program`, `ProgramItem`, `EventDuty`.

Entities without lifecycle state: `IdentifierHistory`, `Eligibility`, `SystemSetting`, `Admin`, `D10` lookup values.

---

## Universal invariants

These hold across all five configuration processes:

1. **Rule neutrality.** No configuration process creates a link between `Role` and `Duty` except through a `DutyRule` row written by 2.0. No configuration process pre-associates any entity with any other.
2. **Deactivation, not deletion.** Configuration entities are never removed.
3. **No backward propagation.** Editing any configuration row affects future use only. No operational record is ever rewritten by a configuration change.
4. **Referential integrity.** Every foreign key references an existing row.
5. **No evaluation.** No configuration process evaluates any rule, threshold, or criterion.
6. **No orchestration.** No configuration process invokes any other process.
7. **Authorization upstream.** Configuration processes assume the caller has passed the applicable `PermissionTier` check.
8. **Immediate activation.** Created entities are active from creation, where the entity has lifecycle state.

---

## The 8.0 exception

8.0 Manage Events and Programs is the only configuration process that writes to an operational store. When a Program Item is linked to a Service Definition — at creation or by later edit — the system creates an event-sourced `ServiceOccurrence` row with:

- `EventID` populated
- `ScheduleID` null
- `GeneratedBy = Administrator`
- `CreatedBy` = the admin who created the Program Item

This is a deliberate exception. Event-sourced occurrences bypass the Occurrence Materializer entirely; their lifecycle is governed by the Event/Program process, not by 11.0.

---

## Reading order

For a reader new to the configuration layer:

1. **This README** — the layer overview.
2. **`1.0-configure-vocabulary.md`** — the process that establishes the vocabulary.
3. **`2.0-configure-duty-rules.md`** — the process that expresses how duties rank candidates.
4. **`3.0-manage-membership.md`** — the process that maintains the member register.
5. **`4.0-manage-eligibility.md`** — the process that grants and revokes candidacy.
6. **`8.0-manage-events-and-programs.md`** — the process that manages the event lifecycle.

---

*Source: System Design Specification §9.*
