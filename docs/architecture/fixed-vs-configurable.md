# Fixed vs Configurable

*What the system knows without being told, and what an administrator must decide. Derived from the System Design Specification §2, §5, and §6. Where this document conflicts with the specification, the specification wins.*

---

## The distinction

Every part of the system falls into one of two categories:

- **Fixed** — the system knows this without any administrator input. It is built into the architecture.
- **Configurable** — the administrator decides this. It is entered as data.

The line between them is precise. The system knows *what kinds of things exist*; it does not know *which instances exist* or *how they relate*. The administrator supplies every instance and every relationship.

---

## What is fixed

The system knows, without being told:

### The entity vocabulary

That there is a thing called a Member, a Role, a Duty, a Service Definition, a Service Schedule, a Service Occurrence, an Eligibility record, a Duty Rule, a Roster Assignment, an Attendance Record, an Event, a Program. The schema cannot be built otherwise — a table for a thing that does not exist as a category is meaningless.

### The cardinalities and shape of each entity

A Member has exactly one BranchID. A ServiceDefinitionDuty row has exactly one RequiredSlotCount. A RosterAssignment has exactly one OccurrenceID. These are baked into the columns.

### The permitted relationships (the join paths)

DutyRule has a DutyID foreign key. That means the schema knows a Duty Rule *can* be attached to a Duty. It does not mean any Duty has any Rule. The foreign key declares the permitted shape of a relationship, not the existence of one.

### The criteria-type vocabulary

CriteriaType is constrained to one of a fixed set: Role, Gender, Age Range, Tenure, Membership Stage, Branch-Attendance Recency, Eligibility Flag, plus three reserved types. The schema knows *these are the kinds of criteria that can exist*.

### The mechanical operations

- The Occurrence Materializer knows how to read ServiceSchedule, project forward N days, and create ServiceOccurrence rows. That is a mechanical procedure the schema supports structurally.
- Program Item status is computed from scheduled times — a mechanical derivation.
- Tier evaluation is a procedure the engine knows how to run. It reads DutyRule rows if they exist and does nothing rule-based if they do not.

### The direction and timing of data flows

GeneratedBy is distinct from ChangedBy. AssignedBy is populated only when AssignmentSource = Manual. EligibilityID is durable. These are baked-in truths about how the system operates on data, independent of any administrator choice.

### What is not allowed to be known

The schema deliberately refuses to encode that a Role performs a Duty. There is no RoleID on Duty. The absence is a structural fact — the schema knows that it does not know this, and refuses to let any other part of the system assume it either.

---

## What is configurable

The administrator decides, via data entry:

### Which instances exist

Which Roles exist. Which Duties exist. Which Branches exist. Which Members exist. Which Services exist. Which Events exist.

### How they relate

Which Duty Rules exist. Which Members hold which Roles. Which Members belong to which Branches. Which Duties a Service requires. Which Time Slots a Service runs in. Which Events contain which Program Items.

### Which criteria apply, where, and in what order

Which Duty Rules apply to which Duty. What TierOrder each occupies. What CriteriaValue each carries.

### The values

Which members are eligible for which duties. What the RequiredSlotCount is for each duty on each service. What the StartTime is for each schedule. What the horizon is, what the tenure threshold is, what the confirmation timeout is.

### The lookup vocabulary

Which Outcome States exist. Which Assignment Statuses exist. Which Permission Tiers exist. Which TimeOfDay categories exist. Which ServiceTypes exist. Each is an open list, populated by the administrator.

---

## The complete boundary

| Category | Fixed | Configurable |
| --- | --- | --- |
| **Entities** | Table structure, columns, types | Rows |
| **Relationships** | Which FKs exist, what they can connect | Which specific connections exist |
| **Cardinality** | One Member → one BranchID | Whether any specific Branch is populated |
| **Criteria vocabulary** | The seven supported criteria types | Which criteria apply to which duty, in what tier order |
| **Role-Duty link** | That a DutyRule can express Role as a criterion | Whether any Duty uses Role as a criterion, and to what value |
| **Mechanical operations** | How to project a recurring schedule forward N days | What N is |
| **Provenance** | GeneratedBy is permanent | Whether a specific occurrence was system- or admin-generated |
| **Lookups** | That lookup tables exist and are referenced | Which lookup values exist |

---

## The test for any new feature

When evaluating whether a proposed change respects the fixed/configurable separation, ask:

**Does the change add a new instance, or a new kind of thing?**

- A new instance (a new Role, a new Duty, a new Member) → configurable. No schema change needed.
- A new kind of thing (a new criterion type, a new entity, a new relationship shape) → fixed. Requires a specification change.

**Does the change express an organizational rule, or describe a mechanical operation?**

- An organizational rule (Duty X requires Role Y) → configurable. Expressed as data.
- A mechanical operation (project schedules forward N days) → fixed. It is part of how the engine works.

---

## The rule about required settings

One further fixed property: **required configuration that a process depends on must fail loudly when absent, never idle silently.**

An unconfigured category is inert and harmless — a Duty with no Duty Rules simply requires nothing. A process that depends on a specific setting and finds it missing must stop and surface that fact.

This is the difference between "not yet configured" (harmless) and "required but missing" (a failure). The system distinguishes them strictly.

---

## What the system will never do

- It will never hardcode any specific Role, Duty, Service, Branch, or rule.
- It will never pre-link any two categories.
- It will never assume organization-specific behavior.
- It will never proceed with a default when a required setting is absent.

---

## What is not in scope

Some things are neither fixed nor configurable — they are simply outside the current specification:

- Authentication and session management
- Authorization / RBAC enforcement beyond the Permission Tier lookup
- Data classification and retention policy
- Support, incident, problem, and change management infrastructure

These are deliberately excluded. They are not the absence of a decision; they are decisions to defer.

---

*Source: System Design Specification §2, §5, §6.*
