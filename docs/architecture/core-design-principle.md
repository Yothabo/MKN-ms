
# Core Design Principle

*The single rule that governs every other decision in the system. Derived from the System Design Specification §2 and §6. Where this document conflicts with the specification, the specification wins.*

---

## The rule

The system separates two kinds of decisions, and never allows them to collapse into each other:

**Categories** are the fixed concepts the system is built from. They exist because a rostering system needs a vocabulary to represent anything at all. They carry no organizational meaning by themselves.

**Configuration** is every relationship, threshold, priority, permission, and outcome definition. It is entered entirely by an administrator, stored as data, never hardcoded.

An unconfigured category is empty and inert. A Duty with no Duty Rules attached requires nothing and permits nothing until an administrator says otherwise.

---

## Why this matters

The system exists to serve organizations whose rules differ from each other. If it hardcoded any organization's operating rules, it would be useless to any other organization — and it would be brittle for the one it was written for, since organizations change their rules.

By separating vocabulary from configuration, the system can express any organization's rules within whatever categories it supports, without ever needing to be modified for a specific organization.

---

## The absolute prohibition

**No category is pre-linked to another by the system itself.**

This is not a preference. It is a rule with no exceptions.

Specifically:

- **Role and Duty have no relationship** until an administrator creates one.
- The system does not know, assume, or imply that any role performs any duty.
- The relationship exists only as rows in the Duty Rule table, created by an administrator.

The same holds for every other pair of categories. Member and Duty, Member and Service, Service and Branch — none of these have a built-in relationship. Where a relationship exists, it is a row that an administrator created.

---

## The five-layer chain

The architecture, end to end, is a chain of five distinct layers. No layer is allowed to collapse into another.

```

System vocabulary → stored facts → admin configuration → engine interpretation → operational outcomes

```

### 1. System vocabulary

The entity definitions. Fixed. This is what §4 of the specification describes — the tables, the columns, the types.

### 2. Stored facts

Data the administrator enters directly and that is not interpretive: Member records, Identifier History, Branch definitions.

### 3. Admin configuration

The relationships and rules the administrator expresses using the vocabulary: Duty Rules, Eligibility grants, Service Definitions, Schedules, Lookup values.

### 4. Engine interpretation

The operational processes reading configuration and stored facts at run time. The engine knows only how to execute configuration — it never contains any organization's operating rules itself.

### 5. Operational outcomes

What has actually happened: materialized Occurrences, Roster Assignments, Attendance Records, fill status.

---

## The four categories

When reasoning about any part of the system — a schema decision, a process, a piece of configuration — it is worth holding four distinct things separately.

### Fixed

What the system can represent, and what the engine knows how to mechanically calculate.

Examples: the entity set, the criteria-type vocabulary, the mechanism for tier evaluation, the mechanism for materializing occurrences from a recurring schedule.

### Configurable

What an administrator chooses to express using that capability.

Examples: which roles exist, which duties exist, which duties prefer which roles, which services run at which branches, which members hold which roles.

### Operational data

What has actually happened.

Examples: Member records, Attendance Records, Roster Assignments, materialized Service Occurrences.

### MKN configuration

The particular values MKN administrators eventually enter. Documented in the Configuration Reference — never in the System Design Specification itself.

---

## The rule about required settings

One further rule derives from the core principle:

**Required configuration that a process depends on must fail loudly when absent, never idle silently.**

An unconfigured category is inert and harmless — a Duty with no Duty Rules simply requires nothing. But a process that depends on a specific setting to run correctly and finds that setting missing must stop and surface that fact, not proceed as if nothing were wrong.

This is the difference between "not yet configured" (harmless, inert) and "required but missing" (a failure that must be reported). The system distinguishes them strictly.

---

## What this principle forbids

The core principle forbids the following, without exception:

- Hardcoding any role, duty, service, branch, or rule into the system.
- Pre-linking any two categories.
- Assuming any organization-specific behavior.
- Merging the engine's interpretation with the configuration it interprets.
- Letting a process proceed with a default when a required setting is absent.

---

## What this principle requires

- Every relationship between categories is a row an administrator created.
- Every process reads configuration at run time; none has it embedded.
- Every required setting's absence is reported, never silently worked around.
- Every derived document in this tree obeys the same separation.

---

## How the principle shows up in the rest of the specification

- **§4** defines the categories — the fixed vocabulary.
- **§5** defines the configuration language — what an administrator can express.
- **§7** resolves the one process that most clearly needed the fixed/configurable distinction applied: Occurrence Materialization, which is a mechanical operation on configuration, not a configuration choice.
- **§8–§17** derive the operational contracts, each respecting the separation.
- **§15** consolidates the schema amendments, all of which are vocabulary, not rules.
- **§16** states the invocation topology, which is a mechanical property, not an organizational choice.

---

*Source: System Design Specification §2, §6.*
