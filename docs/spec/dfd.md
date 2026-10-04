
# MKN-MS — Data Flow Diagrams

*Scoped strictly to the current System Design Specification. No security, sessions, authorization, data-retention, or support/incident infrastructure.*

Standard DFD notation: rectangles are external entities, circles are processes (numbered), cylinders are data stores (D#).

---

## Level 0 — Context Diagram

```mermaid
flowchart TB
    Admin[Administrator]
    Member[Member]
    Sys((0.0 MKN-MS))

    Admin -->|Configuration data| Sys
    Sys -->|Rosters, fill-status, confirmations| Admin
    Member -->|Attendance taps, responses| Sys
    Sys -->|Assignment notices, schedule display| Member
```

---

Level 1 — Process Decomposition

Twelve processes. 11.0 Materialize Occurrences is system/time-triggered, with an optional manual trigger from Administrator. 12.0 Create Manual Assignment is administrator-triggered, with no scheduled component. These are the only two processes without a required external-entity input on their primary trigger path.

```mermaid
flowchart TB
    Admin[Administrator]
    Member[Member]

    subgraph Configuration
        P1((1.0 Configure vocabulary))
        P2((2.0 Configure duty rules))
        P4((4.0 Manage eligibility))
        P8((8.0 Manage events and programs))
    end

    subgraph Membership
        P3((3.0 Manage membership))
    end

    subgraph Operations
        P5((5.0 Generate assignment))
        P6((6.0 Record attendance))
        P7((7.0 Manage confirmation))
        P9((9.0 Dispatch notification))
        P10((10.0 Evaluate fill status))
        P11((11.0 Materialize occurrences))
        P12((12.0 Create manual assignment))
    end

    D1[(D1 Role / Duty)]
    D2[(D2 Duty Rule)]
    D3[(D3 Branch / Time Slot / Service / Occurrence)]
    D4[(D4 Member)]
    D5[(D5 Identifier History)]
    D6[(D6 Eligibility)]
    D7[(D7 Roster Assignment)]
    D8[(D8 Attendance Record)]
    D9[(D9 Event / Program / Item / Event Duty)]
    D10[(D10 Config Lookups)]
    D11[(D11 System Setting)]
    D12[(D12 Materializer Run Log)]

    Admin -->|Define roles, duties, branches, services| P1
    P1 --> D1
    P1 --> D3
    P1 --> D10

    Admin -->|Define tiers and criteria| P2
    D1 -.->|Reads| P2
    P2 --> D2

    Admin -->|Create or edit member| P3
    P3 --> D4
    P3 --> D5

    Admin -->|Grant or revoke| P4
    P4 --> D6

    D2 -.->|Reads| P5
    D3 -.->|Reads| P5
    D4 -.->|Reads| P5
    D6 -.->|Reads| P5
    P5 --> D7
    P5 -->|Generated roster| Admin

    D4 -.->|Reads| P6
    D3 -.->|Reads| P6
    Member -->|NFC tap| P6
    P6 --> D8

    Member -->|Confirm / decline| P7
    D7 -.->|Reads| P7
    P7 --> D7

    D7 -.->|New / changed assignment| P9
    P9 -->|Assignment notice| Member

    D7 -.->|Reads| P10
    D3 -.->|Reads| P10
    D10 -.->|Reads| P10
    P10 --> D3

    D11 -.->|Reads horizon| P11
    D3 -.->|Reads active schedules| P11
    Admin -.->|Optional manual trigger with explicit horizon| P11
    P11 -->|Creates missing occurrences only| D3
    P11 --> D12

    Admin -->|Create event, program, item, duty| P8
    P8 --> D9
    P8 -.->|Creates event-sourced occurrence directly| D3

    Admin -->|Create manual assignment| P12
    D2 -.->|Reads Eligibility Flag tiers| P12
    D3 -.->|Reads occurrence| P12
    D4 -.->|Reads member| P12
    D6 -.->|Reads eligibility grant| P12
    D7 -.->|Uniqueness check| P12
    P12 -->|Creates manual assignment| D7
    P12 -.->|Invokes only when AssignmentStatusID is NULL| P9
```

---

Level 2 — Configure Vocabulary (1.0)

```mermaid
flowchart TB
    Admin[Administrator]
    subgraph "1.0 Configure vocabulary"
        P11((1.1 Configure role))
        P12((1.2 Configure duty))
        P13((1.3 Configure branch))
        P14((1.4 Configure time slot))
        P15((1.5 Configure service definition))
        P16((1.6 Configure service duty and schedule))
    end
    D1[(D1 Role / Duty)]
    D3[(D3 Branch / Time Slot / Service / Occurrence)]

    Admin --> P11 --> D1
    Admin --> P12 --> D1
    Admin --> P13 --> D3
    Admin --> P14 --> D3
    Admin --> P15 --> D3
    D3 -.->|Reads| P16
    D1 -.->|Reads| P16
    P16 --> D3
```

---

Level 2 — Manage Membership (3.0)

```mermaid
flowchart TB
    Admin[Administrator]
    subgraph "3.0 Manage membership"
        P31((3.1 Manage member record))
        P32((3.2 Manage identifier history))
    end
    D4[(D4 Member)]
    D5[(D5 Identifier History)]

    Admin --> P31 --> D4
    Admin --> P32
    D5 -.->|Reads| P32
    P32 --> D5
```

---

Level 2 — Generate Assignment (5.0)

Mirrors the Eligibility → Priority → Assignment component chain directly.

```mermaid
flowchart TB
    subgraph "5.0 Generate assignment"
        P51((5.1 Filter eligible candidates))
        P52((5.2 Order by priority tiers))
        P53((5.3 Fill occurrence slots))
    end
    D4[(D4 Member)]
    D6[(D6 Eligibility)]
    D2[(D2 Duty Rule)]
    D3[(D3 Branch / Time Slot / Service / Occurrence)]
    D7[(D7 Roster Assignment)]
    Admin[Administrator]

    D4 -.->|Reads| P51
    D6 -.->|Reads| P51
    P51 -->|Candidate set| P52
    D2 -.->|Reads| P52
    P52 -->|Ranked candidates| P53
    D3 -.->|Reads| P53
    P53 --> D7
    P53 -->|Generated roster| Admin
```

---

Level 2 — Materialize Occurrences (11.0)

The fully specified contract from the System Design Specification §7, as a flow.

```mermaid
flowchart TB
    subgraph "11.0 Materialize occurrences"
        P111((11.1 Read horizon))
        P112((11.2 Resolve system today))
        P113((11.3 Find active schedules))
        P114((11.4 Generate candidate dates per schedule))
        P115((11.5 Check existing occurrence))
        P116((11.6 Create occurrence if missing))
        P117((11.7 Record run result))
    end
    D11[(D11 System Setting)]
    D3[(D3 Branch / Time Slot / Service / Occurrence)]
    D12[(D12 Materializer Run Log)]
    Admin[Administrator]

    D11 -.->|Horizon, required — missing value halts with a configuration error| P111
    P111 --> P112
    P112 --> P113
    D3 -.->|Active schedules only| P113
    P113 --> P114
    P114 --> P115
    D3 -.->|Reads by ScheduleID + Date| P115
    P115 -->|Exists: skip| P117
    P115 -->|Missing: proceed| P116
    P116 -->|Inherits service type and time; GeneratedBy = System| D3
    P116 --> P117
    P117 --> D12
    Admin -.->|Optional: manual trigger, explicit horizon, identical logic| P111
```

Every existing occurrence this process encounters is left untouched — the only output to D3 is the creation of rows that did not previously exist.

---

Process Dictionary

# Process Reads from Writes to
1.0 Configure vocabulary — D1, D3, D10
2.0 Configure duty rules D1 D2
3.0 Manage membership — D4, D5
4.0 Manage eligibility — D6
5.0 Generate assignment D2, D3, D4, D6, D7 D7
6.0 Record attendance D4, D3 D8
7.0 Manage confirmation D7, D10, D11 D7
8.0 Manage events and programs — D9, D3 (event-sourced occurrences)
9.0 Dispatch notification D7, D4, D3, D11 — (outbound to Member)
10.0 Evaluate fill status D7, D3, D10, D11 D3

11.0 Materialize occurrences D11, D3 D3, D12
12.0 Create manual assignment D2, D3, D4, D6, D7 D7

Data Store Dictionary

Store Contents
D1 Role, Duty
D2 Duty Rule
D3 Branch, Branch Time Slot, Service Definition, Service Definition Duty, Service Schedule, Service Occurrence, Service Occurrence Duty
D4 Member
D5 Identifier History
D6 Eligibility
D7 Roster Assignment
D8 Attendance Record
D9 Event, Program, Program Item, Event Duty
D10 Outcome State, Assignment Status, Permission Tier, TimeOfDay, ServiceType
D11 System Setting
D12 Materializer Run Log

---

Resolved design decisions

· Occurrence materialization is a system-internal, daily-triggered, idempotent process on a rolling horizon, fully specified in §7 of the System Design Specification and shown as process 11.0 above.
· Duty Rule tier composition supports multiple criteria per tier via same-TierOrder rows, combined with AND; no OR or NOT at this stage.

What was deliberately excluded

Authentication, session management, authorization/RBAC, privilege escalation protection, data classification and retention policy, and a full support/incident/problem/change/maintenance practice. None of that is represented here.
