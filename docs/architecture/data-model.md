# Data Model

*The entities of the system and the relationships between them. Derived from the System Design Specification §4 and §15. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document describes the shape of the data the system stores — the entities, their attributes in outline, and how they relate. It is a navigational view. For the complete column-level reference, see `entity-reference.md`. For the schema used at implementation, see `../database/schema.md`.

---

## The stores

The system groups its data into twelve conceptual stores. Each holds a distinct set of entities.

| Store | Contents |
| --- | --- |
| D1 | Role, Duty |
| D2 | Duty Rule |
| D3 | Branch, Branch Time Slot, Service Definition, Service Definition Duty, Service Schedule, Service Occurrence, Service Occurrence Duty |
| D4 | Member, Member Status, Attribute Type, Member Attribute Value, Admin |
| D5 | Identifier History |
| D6 | Eligibility |
| D7 | Roster Assignment |
| D8 | Attendance Record |
| D9 | Event, Event Branch, Program, Program Item, Event Duty |
| D10 | Outcome State, Assignment Status, Permission Tier, TimeOfDay, ServiceType, Capability |
| D11 | System Setting |
| D12 | Materializer Run |

A configuration-audit store is also present. It holds Configuration Audit Log, Entity Deletion Policy, and Notification Subscription. These entities are not part of D1 through D12; they record the configuration lifecycle and its notification fan-out.

---

## The entity relationships

The diagram shows the primary relationships. Direction of the arrows is from the referencing entity to the referenced entity. The full set of foreign keys and their targets is in `entity-reference.md`.

~~~mermaid
erDiagram
    Branch ||--o{ BranchTimeSlot : has
    Branch ||--o{ Member : "home for"
    Branch ||--o{ ServiceDefinition : "may own"
    Branch ||--o{ Event : hosts
    Branch ||--o{ EventBranch : attends

    TimeOfDay ||--o{ BranchTimeSlot : classifies

    ServiceType ||--o{ ServiceDefinition : classifies

    ServiceDefinition ||--o{ ServiceDefinitionDuty : requires
    Duty ||--o{ ServiceDefinitionDuty : "required by"

    ServiceDefinition ||--o{ ServiceSchedule : "scheduled as"
    BranchTimeSlot ||--o{ ServiceSchedule : "scheduled at"

    ServiceSchedule ||--o{ ServiceOccurrence : "materializes to"

    ServiceOccurrence ||--o{ ServiceOccurrenceDuty : "overrides"
    Duty ||--o{ ServiceOccurrenceDuty : "overridden by"

    ServiceOccurrence ||--o{ RosterAssignment : "assigned on"
    ServiceOccurrence ||--o{ AttendanceRecord : "attended at"

    OutcomeState ||--o{ ServiceOccurrence : "fill status"

    Role ||--o{ Member : "held by"
    Role ||--o{ DutyRule : "referenced by"

    Duty ||--o{ DutyRule : ranks
    Duty ||--o{ Eligibility : "granted for"
    Duty ||--o{ RosterAssignment : "assigned as"
    Duty ||--o{ EventDuty : "required by"

    Member ||--o{ IdentifierHistory : has
    Member ||--o{ Eligibility : "granted to"
    Member ||--o{ RosterAssignment : "assigned as"
    Member ||--o{ AttendanceRecord : attends
    Member ||--o| Admin : "may be"
    Member ||--o{ MemberAttributeValue : has
    MemberStatus ||--o{ Member : "status of"

    AttributeType ||--o{ MemberAttributeValue : classifies

    PermissionTier ||--o{ Admin : classifies
    PermissionTier ||--o{ EntityDeletionPolicy : "required by"
    PermissionTier ||--o{ NotificationSubscription : "recipient tier"

    AssignmentStatus ||--o{ RosterAssignment : "status of"

    Event ||--o| Program : "has one"
    Event ||--o{ EventBranch : "attended at"
    Program ||--o{ ProgramItem : contains
    ProgramItem ||--o{ EventDuty : "carries"
    ProgramItem }o--o| ServiceDefinition : "may link to"
    Member ||--o{ EventDuty : "directly assigned"
    EventDuty }o--|| Duty : references

    Event ||--o{ ServiceOccurrence : "event-sourced"

    Admin ||--o{ IdentifierHistory : authorizes
    Admin ||--o{ Eligibility : grants
    Admin ||--o{ MaterializerRun : "may trigger"
    Admin ||--o{ ServiceOccurrence : "may create"
    Admin ||--o{ MemberAttributeValue : records
    Admin ||--o{ ConfigurationAuditLog : initiates
    Admin ||--o{ ConfigurationAuditLog : approves
    Admin ||--o{ NotificationSubscription : "recipient admin"
~~~

---

## The entity groups

### Configuration entities

Entities the administrator creates to define the organization's vocabulary.

- **Role** — a named category a member may hold.
- **Duty** — a named responsibility that may be required of a service.
- **Branch** — a physical location where services run.
- **Branch Time Slot** — a recurring day-and-time-of-day at a branch.
- **Service Definition** — a named template for a recurring service.
- **Service Definition Duty** — a duty required by a service, with a slot count.
- **Service Schedule** — an active instance of a service at a specific time slot, with a start time.

### Rule entities

Entities the administrator creates to express how duties are ranked and who is eligible.

- **Duty Rule** — a tier and criteria that rank candidates for a duty.
- **Eligibility** — a grant/revoke record making a member a candidate for a duty.

### Membership entities

Entities representing members and their identifiers.

- **Member** — a person in the organization's register. Carries a `MemberStatusID`, a `ReceiptNumber`, a `CardNumber`, and a `JoinReason`.
- **Member Status** — the administrator-defined vocabulary of member states, with an `IsRosterable` flag.
- **Identifier History** — the log of identifier assignments (cards, receipts, and any other admin-defined types).
- **Attribute Type** — the administrator-defined vocabulary of member attribute names.
- **Member Attribute Value** — one value per member per attribute set.

### Administrative entities

- **Admin** — links a member to a permission tier. An Admin row records that a member has been granted admin status by another admin.
- **Permission Tier** — an administrator-defined name for a capability level. The system names no tier.
- **Capability** — the vocabulary of named actions the system can perform. No assignment of capabilities to roles or admins is modelled.

### Operational entities

Entities produced by the system's operational processes.

- **Service Occurrence** — a materialized instance of a scheduled service on a specific date.
- **Service Occurrence Duty** — a per-occurrence override of the service's normal duty list.
- **Roster Assignment** — a member assigned to a duty on a specific occurrence.
- **Attendance Record** — a recorded fact of a member's presence at an occurrence.
- **Materializer Run** — a log record for each Occurrence Materializer run.

### Event entities

- **Event** — a bounded, dated occurrence with a name and a host branch. The `Location` field is not carried on the Event.
- **Event Branch** — the set of branches attending an event. Determines roster scope across branches.
- **Program** — an ordered schedule belonging to an event.
- **Program Item** — a single entry in a program, optionally linked to a service definition. May carry a finer `Location` than the branch.
- **Event Duty** — an event's requirement for a real Duty, with a required slot count and an optional Service Definition scope.

### Lookup entities

Open, administrator-defined vocabularies referenced by other entities.

- **Outcome State** — the possible fill states of an occurrence.
- **Assignment Status** — the possible statuses of a roster assignment.
- **Permission Tier** — the possible permission tiers.
- **TimeOfDay** — the coarse time-of-day classification on a branch time slot.
- **ServiceType** — the classification of a service definition.

### Configuration-lifecycle entities

Entities that record the two-flag lifecycle and its notification fan-out.

- **Configuration Audit Log** — the permanent record of every deactivation and every soft delete.
- **Entity Deletion Policy** — the per-entity-type configuration of the deletion model: approval requirement, required permission tier, and reason requirement.
- **Notification Subscription** — the recipient configuration for authority notifications. Either a permission tier or a specific admin, never both.

### Settings

- **System Setting** — scalar, non-list configuration values. Horizons, thresholds, durations, and the mapping settings that connect mechanical conditions to administrator-defined vocabulary.

---

## The key relationships

A few relationships are worth calling out because they define how the system behaves.

### Role and Duty — no direct link

Role and Duty have no direct relationship in the schema. A Duty carries no RoleID, and a Role carries no DutyID. The relationship — when one exists — is expressed as a row in the Duty Rule table, with CriteriaType = Role. This is a deliberate consequence of the core design principle.

### Member, Eligibility, and Duty

A member's candidacy for a duty is determined by the Eligibility table. Absence of a row means the member is not eligible. Multiple rows for the same (Member, Duty) pair represent grant/revoke history.

### Service Schedule — the join of Service Definition and Branch Time Slot

A schedule is what actually runs. It connects a service definition to a time slot, carries the concrete StartTime, and has an IsActive flag. The pair (ServiceDefID, TimeSlotID) is unique among active schedules.

### Service Occurrence — materialized or event-sourced

A Service Occurrence may originate from a Service Schedule (schedule-sourced) or from a Program Item linked to a Service Definition (event-sourced). Schedule-sourced occurrences carry ScheduleID; event-sourced occurrences carry EventID. The two are distinguished by which of the two foreign keys is populated.

### Roster Assignment — one member, one duty, one occurrence

The uniqueness constraint on (MemberID, DutyID, OccurrenceID) prevents duplicate assignment records. The AssignmentStatusID is nullable because 5.0 creates assignments with no status; the assignment lifecycle begins with 7.0.

### Attendance Record — one fact per member per occurrence

The uniqueness constraint on (MemberID, OccurrenceID) makes attendance a single fact per member per occurrence, not a tap log.

---

## The amendment set

The base schema is defined in §4 of the System Design Specification. A small set of additions is defined in §15 — two columns, four constraints, eight settings, one nullability clarification. The amendments are:

- RosterAssignment.CreatedAt — a new required timestamp.
- AssignmentStatus.IsTerminal — a new required boolean.
- RosterAssignment (MemberID, DutyID, OccurrenceID) unique.
- AttendanceRecord (MemberID, OccurrenceID) unique.
- ServiceSchedule (ServiceDefID, TimeSlotID) unique for active rows.
- ServiceOccurrence (ScheduleID, Date) unique for schedule-sourced rows.
- Eight SystemSetting keys.
- RosterAssignment.AssignmentStatusID — nullable clarification.

The full list, with sources, is in `../database/amendments.md`.

---

## What is not modeled

- No MemberRole join table. A member holds exactly one role.
- No MemberBranch join table. A member belongs to exactly one branch.
- No status history for assignments. Only the current status is stored.
- No notification log. Notifications are fire-and-forget.
- No attendance-event table. One row per member per occurrence.
- No tombstone table. Configuration entities are deactivated, not deleted.
- No orchestration entity. The three invocation edges are process-to-process, not mediated.

Each of these is a deliberate decision, not an oversight.

---

*Source: System Design Specification §4, §15.*
