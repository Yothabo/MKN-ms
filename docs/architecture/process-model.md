# Process Model

*The thirteen processes that make up the system, grouped by layer. Derived from the System Design Specification §3 and the Data Flow Diagrams. Where this document conflicts with the specification, the specification wins.*

---

## Overview

The system is composed of thirteen processes. Each has a defined trigger, a defined boundary, a defined read and write footprint, and a defined set of invariants. No process embeds an organization's rules; each reads configuration at run time.

The processes divide into two layers:

- **Configuration** — processes that create and maintain the administrator-entered vocabulary and rules.
- **Operations** — processes that produce operational records from configuration and stored facts.

The distinction is load-bearing. Configuration processes write to configuration stores. Operational processes read configuration and produce operational data. The two layers meet only at the boundary where an operational process reads configuration.

---

## Configuration layer

Five processes create and maintain every administrator-entered value in the system.

### 1.0 Configure Vocabulary

Creates and maintains Role, Duty, Branch, Branch Time Slot, TimeOfDay, ServiceType, Service Definition, Service Definition Duty, Service Schedule, and the D10 lookup values (Outcome State, Assignment Status, Permission Tier).

**Trigger:** Administrator action.

**Reads:** Nothing, except existing vocabulary for display and D10 lookups during subprocesses.

**Writes:** D1, D3, D10.

**Subprocesses:** 1.1 Configure role, 1.2 Configure duty, 1.3 Configure branch, 1.4 Configure time slot, 1.5 Configure service definition, 1.6 Configure service duty and schedule.

### 2.0 Configure Duty Rules

Creates and maintains the Duty Rule rows that express how duties rank candidates.

**Trigger:** Administrator action.

**Reads:** D1 (Role, Duty for selection).

**Writes:** D2.

### 3.0 Manage Membership

Creates and maintains Member records and Identifier History.

**Trigger:** Administrator action.

**Reads:** D4, D5, D1 (Role), D3 (Branch).

**Writes:** D4, D5.

**Subprocesses:** 3.1 Manage member record, 3.2 Manage identifier history.

### 4.0 Manage Eligibility

Creates and maintains the Eligibility grant/revoke records that determine duty candidacy.

**Trigger:** Administrator action.

**Reads:** D4, D1 (Duty), D6.

**Writes:** D6.

### 8.0 Manage Events and Programs

Creates and maintains Event, Program, Program Item, and Event Duty records. When a Program Item is linked to a Service Definition, the system creates an event-sourced Service Occurrence directly.

**Trigger:** Administrator action.

**Reads:** D9, D3 (Service Definition), D4 (Member), D10 (Assignment Status).

**Writes:** D9, D3 (event-sourced occurrences only).

**Note:** 8.0 is the sole configuration process permitted to create an operational record, and only because a Program Item has been explicitly linked to a Service Definition.

---

## Operations layer

Seven processes produce operational records from configuration and stored facts.

### 5.0 Generate Assignment

Fills the required duty slots of a Service Occurrence by creating Roster Assignment records. Uses Eligibility to determine candidacy and Duty Rule to rank candidates.

**Trigger:** Scheduled run; manual administrator action.

**Reads:** D2, D3, D4, D6, D7, and D8 (only when a Branch-Attendance Recency criterion is present).

**Writes:** D7.

**Invokes:** 9.0 Dispatch Notification, on each new automatic assignment.

**Subprocesses:** 5.1 Filter eligible candidates, 5.2 Order by priority tiers, 5.3 Fill occurrence slots.

### 6.0 Record Attendance

Records the raw fact of a member's presence at an occurrence.

**Trigger:** Member action (NFC tap or configured channel); manual administrator entry.

**Reads:** D4, D3.

**Writes:** D8.

### 7.0 Manage Confirmation

Tracks each Roster Assignment's response lifecycle. On decline or timeout, re-resolves only the affected slot.

**Trigger:** Member response; scheduled timeout check.

**Reads:** D7, D10, D11, D4, and (during re-resolution) D2, D3, D6, D8.

**Writes:** D7.

**Invokes:** 9.0 Dispatch Notification, on each replacement assignment.

### 9.0 Dispatch Notification

Sends assignment notices to members through the configured channel.

**Trigger:** Invocation by 5.0, 7.0, 12.0, or 13.0. 5.0 and 7.0 invoke it on new-assignment creation. 12.0 invokes it when the manual assignment is created with AssignmentStatusID = NULL. 13.0 invokes it when an Attendance Rule's outcome is Notify.

**Reads:** D7, D4, D3, D11.

**Writes:** Nothing.

**Does not invoke:** Any process.

### 10.0 Evaluate Fill Status

Computes each occurrence's fill state and writes it to FillStatusID.

**Trigger:** After assignment changes; scheduled sweep.

**Reads:** D7, D3, D10, D11.

**Writes:** D3 (FillStatusID only).

### 11.0 Materialize Occurrences

Generates Service Occurrence records from active Service Schedules on a rolling time horizon. Additive and idempotent — never modifies or deletes an existing occurrence.

**Trigger:** Scheduled run (daily); manual administrator action with an explicit horizon.

**Reads:** D3, D11.

**Writes:** D3, D12.

**Subprocesses:** 11.1 Read horizon, 11.2 Resolve system today, 11.3 Find active schedules, 11.4 Generate candidate dates, 11.5 Check existing occurrence, 11.6 Create occurrence if missing, 11.7 Record run result.

### 12.0 Create Manual Assignment

Directly creates a Roster Assignment from an administrator's selection, bypassing 5.0's tiered candidate ranking while respecting Eligibility Flag criteria and the uniqueness constraint.

**Trigger:** Administrator action. No scheduled component.

**Reads:** D2 (DutyRule — Eligibility Flag tiers only), D3 (ServiceOccurrence), D4 (Member), D6 (Eligibility), D7 (RosterAssignment).

**Writes:** D7.

**Invokes:** 9.0 Dispatch Notification, only when the created row's AssignmentStatusID is NULL.

---

## The full process list

| # | Process | Layer | Trigger | Primary output |
| --- | --- | --- | --- | --- |
| 1.0 | Configure Vocabulary | Configuration | Admin | Roles, Duties, Branches, Time Slots, Services, Schedules, Lookups |
| 2.0 | Configure Duty Rules | Configuration | Admin | Duty Rules |
| 3.0 | Manage Membership | Configuration | Admin | Members, Identifier History |
| 4.0 | Manage Eligibility | Configuration | Admin | Eligibility grants |
| 5.0 | Generate Assignment | Operations | Scheduled / Manual | Roster Assignments |
| 6.0 | Record Attendance | Operations | Member / Admin | Attendance Records |
| 7.0 | Manage Confirmation | Operations | Member / Scheduled | Assignment status changes, replacements |
| 8.0 | Manage Events and Programs | Configuration | Admin | Events, Programs, Program Items, Event Duties, event-sourced Occurrences |
| 9.0 | Dispatch Notification | Operations | Invoked by 5.0 / 7.0 | Outbound message to member |
| 10.0 | Evaluate Fill Status | Operations | After assignment changes / Scheduled | Occurrence FillStatusID |
| 11.0 | Materialize Occurrences | Operations | Scheduled / Manual | Service Occurrences, Materializer Run records |
| 12.0 | Create Manual Assignment | Operations | Admin | Manually-created Roster Assignment |
| 13.0 | Attendance Rule Engine | Operations | Scheduled / Manual | Member status changes, readmission events |

---

## Trigger summary

Every process has at least one trigger. Only 9.0 is both invoked by other processes and carries its own scheduled trigger.

| Trigger type | Processes |
| --- | --- |
| Administrator action | 1.0, 2.0, 3.0, 4.0, 5.0, 8.0, 11.0, 13.0 |
| Scheduled execution | 5.0, 7.0, 10.0, 11.0, 13.0 |
| Member action | 6.0, 7.0 |
| Invoked by another process | 9.0 |

---

## The store footprint

Each process reads from and writes to a defined set of stores. No process writes to a store outside its footprint.

| Process | Reads | Writes |
| --- | --- | --- |
| 1.0 | — | D1, D3, D10 |
| 2.0 | D1 | D2 |
| 3.0 | — | D4, D5 |
| 4.0 | — | D6 |
| 5.0 | D2, D3, D4, D6, D7, D8 (conditional) | D7 |
| 6.0 | D4, D3 | D8 |
| 7.0 | D2 (re-resolution), D3 (re-resolution), D4, D6 (re-resolution), D7, D8 (re-resolution), D10, D11 | D7 |
| 8.0 | D3, D4, D9, D10 | D9, D3 (event-sourced occurrences) |
| 9.0 | D3, D4, D7, D11 | — |
| 10.0 | D3, D7, D10, D11 | D3 |
| 11.0 | D3, D11 | D3, D12 |
| 12.0 | D2, D3, D4, D6, D7 | D7 |
| 13.0 | D2, D3, D4, D5, D7, D10 | D4 |

---

## Process invariants summary

Every process obeys a set of invariants that are locked in the specification and derived in the appendices. The most important ones, grouped:

**Additivity and idempotency:**
- 11.0 is additive and idempotent — it never modifies or deletes an existing occurrence.
- 5.0 is additive — it never removes or modifies an existing assignment.
- 10.0 is idempotent — re-running produces the same FillStatusID.
- 6.0 is idempotent per (MemberID, OccurrenceID).

**Boundary respect:**
- No process writes to a store outside its footprint.
- No configuration process reads an operational store, except 8.0's event-sourced occurrence write.
- No process invokes another process except 5.0 → 9.0, 7.0 → 9.0, 12.0 → 9.0, and 13.0 → 9.0.

**Required-setting discipline:**
- 11.0 refuses to run if `OccurrenceHorizonDays` is unset.
- 7.0 refuses to run if `InitialAssignmentStatusID` is unset, or if `ConfirmationTimeoutHours` is required and unset.
- 10.0 refuses to run if any of the three mechanical mapping settings is unset.
- 9.0 follows the `NotificationChannel` setting's configured validity behavior.

**Lifecycle ownership:**
- 5.0 creates assignments with `AssignmentStatusID = NULL`.
- 7.0 owns the assignment status lifecycle from that point.
- 11.0 creates occurrences with `FillStatusID = NULL`.
- 10.0 owns the fill status lifecycle from that point.

---

*Source: System Design Specification §3, Data Flow Diagrams.*
