# Invocation Model

*How the system's processes communicate. Derived from the System Design Specification §16. Where this document conflicts with the specification, the specification wins.*

---

## The principle

The system's default integration mechanism is **shared data**. Processes read from and write to common data stores. When one process's output needs to reach another process, it does so through a store, not through a call.

Direct process invocation is the exception. It exists in exactly two places, both for the same reason: an outbound message must be sent at the moment an assignment is created, and deferring that to a polling process would require a "notified" flag or a notification queue that the schema deliberately does not have.

Everywhere else, processes are independently executable. No process orchestrates another. The data stores are the interface.

---

## The two direct invocation edges

There are exactly two process-to-process invocations in the system:

| From | To | Trigger |
| --- | --- | --- |
| 5.0 Generate Assignment | 9.0 Dispatch Notification | On each new automatic assignment created |
| 7.0 Manage Confirmation | 9.0 Dispatch Notification | On each replacement assignment created after decline or timeout |

Both invoke 9.0. Both invoke it under the same condition: a new RosterAssignment row has been created. Neither invokes 9.0 for any other reason.

### Why these exist

An assignment notice must be sent at the moment the assignment is created. A polling model would require either a Notified flag or a notification queue — state the schema deliberately does not have. Direct invocation is simpler and bounded: 9.0 is the outbound channel, invoked by the two processes that produce outbound-worthy events.

### Why there are no others

Every other relationship between processes is expressed through shared data:

- 11.0 writes occurrences; 5.0 reads them.
- 5.0 writes assignments; 7.0 and 10.0 read them.
- 7.0 writes status changes; 10.0 reads them.
- 6.0 writes attendance; 5.0 reads it only when required by a criterion.

No process depends on another process's successful completion for its own persisted business result. 5.0 and 7.0 invoke 9.0 as fire-and-forget; whether that invocation is technically synchronous or asynchronous is an implementation choice and does not create a semantic dependency.

---

## The full topology

### Visual form

~~~mermaid
flowchart TB
    C1["1.0 Configure vocabulary"]
    C2["2.0 Configure duty rules"]
    C3["3.0 Manage membership"]
    C4["4.0 Manage eligibility"]
    C8["8.0 Manage events and programs"]

    P11["11.0 Materialize occurrences"]
    P5["5.0 Generate assignment"]
    P7["7.0 Manage confirmation"]
    P10["10.0 Evaluate fill status"]
    P6["6.0 Record attendance"]
    P9["9.0 Dispatch notification"]

    D1[(D1 Role / Duty)]
    D2[(D2 Duty Rule)]
    D3[(D3 Branch / Slot / Service / Occurrence)]
    D4[(D4 Member)]
    D5[(D5 Identifier History)]
    D6[(D6 Eligibility)]
    D7[(D7 Roster Assignment)]
    D8[(D8 Attendance Record)]
    D9[(D9 Event / Program / Item / Event Duty)]
    D10[(D10 Config Lookups)]
    D11[(D11 System Setting)]
    D12[(D12 Materializer Run)]

    C1 -.->|writes| D1
    C1 -.->|writes| D3
    C1 -.->|writes| D10
    C2 -.->|writes| D2
    C3 -.->|writes| D4
    C3 -.->|writes| D5
    C4 -.->|writes| D6
    C8 -.->|writes| D9
    C8 -.->|writes event-sourced occurrence| D3

    D3 -.->|reads active schedules| P11
    D11 -.->|reads horizon| P11
    P11 -.->|writes| D3
    P11 -.->|writes run log| D12

    D2 -.->|reads| P5
    D3 -.->|reads| P5
    D4 -.->|reads| P5
    D6 -.->|reads| P5
    D7 -.->|reads| P5
    P5 -.->|writes| D7
    P5 -->|invokes| P9

    D7 -.->|reads / writes| P7
    D10 -.->|reads| P7
    D11 -.->|reads| P7
    P7 -->|invokes on replacement| P9

    D7 -.->|reads| P10
    D3 -.->|reads| P10
    D10 -.->|reads| P10
    D11 -.->|reads| P10
    P10 -.->|writes FillStatusID| D3

    D4 -.->|reads| P6
    D3 -.->|reads| P6
    P6 -.->|writes| D8

    D7 -.->|reads| P9
    D4 -.->|reads| P9
    D3 -.->|reads| P9
    D11 -.->|reads| P9
~~~

**Legend:** solid arrows (`-->`) are process invocations. Dotted arrows (`-.->`) are data reads or writes. Only two solid arrows exist.

### Exact form

The following topology matches §16 of the System Design Specification. Only connecting arrows are shown.

~~~
1.0 Configure vocabulary ──┐
2.0 Configure duty rules ──┤
3.0 Manage membership ─────┤
4.0 Manage eligibility ────┤
8.0 Manage events/programs ┘
                            │
                            │ writes
                            ▼
        Configuration stores: D1 D2 D3 D4 D5 D6 D9 D10 D11
        (8.0 additionally creates event-sourced ServiceOccurrence
         rows when a ProgramItem is linked to a ServiceDefinition)
                            │
                            │ read by
                            ▼
                  11.0 Materialize Occurrences
                            │
                            │ writes
                            ▼
                  ServiceOccurrence (D3)
                            │
                            │ read by
                            ▼
                  5.0 Generate Assignment
                            │
                   ┌────────┴────────┐
                   │ writes          │ invokes
                   ▼                 ▼
        RosterAssignment (D7)   9.0 Dispatch Notification
                   │                 ▲
                   │ read/written    │
                   ▼                 │
        7.0 Manage Confirmation ─────┘
                   │      invokes (on replacement)
                   │
                   │ feeds
                   ▼
        10.0 Evaluate Fill Status
                   │
                   │ writes
                   ▼
        ServiceOccurrence.FillStatusID


        6.0 Record Attendance
                   │
                   │ writes
                   ▼
        AttendanceRecord (D8)
        (read by 5.0 only when a Branch-Attendance Recency criterion exists)
~~~

---

## The invocation matrix

| Process | Invokes | Invoked by |
| --- | --- | --- |
| 1.0 Configure Vocabulary | — | — |
| 2.0 Configure Duty Rules | — | — |
| 3.0 Manage Membership | — | — |
| 4.0 Manage Eligibility | — | — |
| 5.0 Generate Assignment | 9.0 | — |
| 6.0 Record Attendance | — | — |
| 7.0 Manage Confirmation | 9.0 | — |
| 8.0 Manage Events and Programs | — | — |
| 9.0 Dispatch Notification | — | 5.0, 7.0 |
| 10.0 Evaluate Fill Status | — | — |
| 11.0 Materialize Occurrences | — | — |

**Confirmation of the two-edge claim:**

- 5.0 invokes 9.0. Nothing invokes 5.0.
- 7.0 invokes 9.0. Nothing invokes 7.0.
- 9.0 invokes nothing. It is invoked by 5.0 and 7.0.
- No other process invokes or is invoked by anything.

This is the entire invocation topology of the system.

---

## Process dependency vs. data dependency

These are two different kinds of coupling, and the distinction matters.

**Process dependency** — one process invoking another. The system has exactly two process-dependency edges (5.0 → 9.0 and 7.0 → 9.0).

**Data dependency** — one process requiring records another process has produced. The system has many of these:

- 5.0 requires occurrences (produced by 11.0).
- 7.0 requires assignments (produced by 5.0).
- 10.0 requires assignments and occurrences (produced by 5.0 and 11.0).
- 9.0 requires assignments (produced by 5.0 or 7.0).

Data dependencies do not imply process dependencies. A process that requires records another process has produced simply reads them from the store when it runs. It does not call the other process.

---

## Trigger independence

Every process has an independent trigger mechanism except 9.0.

| Process | Trigger |
| --- | --- |
| 1.0 | Administrator action |
| 2.0 | Administrator action |
| 3.0 | Administrator action |
| 4.0 | Administrator action |
| 5.0 | Scheduled run; manual admin trigger |
| 6.0 | Member action at an occurrence; manual admin entry |
| 7.0 | Member response; scheduled timeout check |
| 8.0 | Administrator action |
| 9.0 | Invocation by 5.0 or 7.0 |
| 10.0 | After assignment changes; scheduled sweep |
| 11.0 | Scheduled run; manual admin trigger |

No process has a trigger that is another process's invocation, except 9.0. This preserves the independently-executable property: each process can be triggered by the administrator, the scheduler, or the member without depending on any other process running first.

---

## Data-store mediation

Cross-process communication happens through stores, not through calls. The following table shows the mediation pattern.

| Process | Reads | Writes |
| --- | --- | --- |
| 5.0 Generate Assignment | D2, D3, D4, D6, D7, D8 (conditional) | D7 |
| 6.0 Record Attendance | D3, D4 | D8 |
| 7.0 Manage Confirmation | D2 (re-res), D3 (re-res), D4, D6 (re-res), D7, D8 (re-res), D10, D11 | D7 |
| 9.0 Dispatch Notification | D3, D4, D7, D11 | — |
| 10.0 Evaluate Fill Status | D3, D7, D10, D11 | D3 |
| 11.0 Materialize Occurrences | D3, D11 | D3, D12 |

The overlap pattern shows the mediation clearly:

- 5.0 writes D7, which 7.0 and 10.0 read.
- 11.0 writes D3 (occurrences), which 5.0, 7.0, and 10.0 read.
- 6.0 writes D8, which 5.0 reads only when required.

In every case, the reader finds what it needs by querying the store, not by being called.

---

## Invariants of the invocation model

- **Two direct invocation edges only.** 5.0 → 9.0 and 7.0 → 9.0.
- **Shared data is the default integration mechanism.** Every other relationship is expressed by one process writing to a store and another reading from it.
- **No orchestration.** No process orchestrates another.
- **No process depends on another process's successful completion for its own persisted business result.** 5.0 and 7.0 invoke 9.0 as fire-and-forget; whether the invocation is synchronous or asynchronous is an implementation choice and does not create a semantic dependency.
- **Every process has an independent trigger except 9.0.**
- **Fire-and-forget notification.** 9.0's success or failure does not affect the invoking process's state.
- **No feedback loops.** The invocation graph is acyclic and terminates at 9.0.

---

## What this model preserves

**Independent execution.** Each process can be scheduled or invoked independently. Their data dependencies do not require process-to-process invocation.

**Independent testability.** Every process can be tested in isolation, because its behavior depends only on the state of the stores it reads and the parameters of its trigger.

**Independent observability.** Every process's behavior is visible through the stores it writes, except 9.0, whose contract explicitly excludes delivery tracking.

---

*Source: System Design Specification §16.*
