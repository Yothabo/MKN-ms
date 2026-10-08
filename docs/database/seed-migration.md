# Seed Migration

*How the existing seed scripts are reconciled with the frozen specification. Derived from the specification and the current seed scripts. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

This document states the strategy for reconciling the seed scripts with the frozen specification. The seed scripts are non-authoritative. They exist to bring a development or test database into a known state. They do not define behaviour; they configure a starting state for testing.

The strategy is a rewrite, not a patch. The existing scripts reflect the pre-amendment schema; the target is the frozen schema. Attempting to patch them produces a script whose structure is half old and half new. A clean rewrite is smaller and clearer.

---

## The current seed scripts

Two scripts exist at the freeze commit:

- `backend/scripts/seed-development.sql` — the minimal configuration to exercise the configuration layer and 11.0.
- `backend/scripts/seed-assignment.sql` — the additional configuration to exercise 5.0's duty-rule evaluation.

Both are idempotent. Both use `NOT EXISTS` guards on natural keys. Both reflect the pre-amendment schema:

- No `MemberStatus` rows.
- No `AttributeType` or `MemberAttributeValue` rows.
- No `EventBranch` rows.
- `EventDuty` rows with `Label` rather than `DutyID`.
- `Member` rows with `IsActive` rather than `MemberStatusID`.
- No settings for the new amendment keys (`DeclinedStatusID`, `TimedOutStatusID`, `YouthAgeMin`, `YouthAgeMax`, the attendance register scope).
- No `AttendanceRule` rows.

---

## The seed design

The rewrite produces three seed scripts, split by concern.

**`seed-vocabulary.sql`** — the base vocabulary every environment needs.

- Lookup rows: `TimeOfDay`, `ServiceType`, `OutcomeState`, `AssignmentStatus`, `PermissionTier`, `MemberStatus`, `Capability`, `AttributeType`.
- Vocabulary rows: `Role`, `Duty`.
- Structural rows: `Branch`, `BranchTimeSlot`, `ServiceDefinition`, `ServiceDefinitionDuty`, `ServiceSchedule`.
- The `SystemSetting` rows with their production defaults: `OccurrenceHorizonDays`, `InitialAssignmentStatusID`, `DeclinedStatusID`, `TimedOutStatusID`, `ConfirmationTimeoutHours`, the four `OutcomeState*ID` keys, `NotificationChannel`, `ReceiptToCardDurationDays`, `YouthAgeMin`, `YouthAgeMax`, and the global attendance register scope.
- One default `Role`, one default `MemberStatus`, one default `PermissionTier`. The defaults are the rows the amendment fallback rules use.

**`seed-membership.sql`** — member register and eligibility.

- Members. Each carries a `MemberStatusID` referencing the default status.
- Identifier history for a subset.
- Admin rows.
- Eligibility grants.
- Member attribute values, where relevant to the duties being exercised.

**`seed-operations.sql`** — the operational configuration that exercises the process contracts.

- Duty rules, with global and per-service rules as the tests require.
- Attendance rules with different trigger types.
- Events, programs, program items, and event duties.
- Additional members where the roster tests require more than the base set.

Splitting the three scripts means a test that needs only vocabulary does not pay for the operational setup, and a test that does not need operational configuration does not carry it.

---

## The rewrite rules

Every rewrite follows the same rules. They are the rules the existing scripts already follow, and they remain correct.

**Idempotency.** Every insert is guarded by a `NOT EXISTS` check on the entity's natural key. The script may be run any number of times; the database ends in the same state.

**Natural-key guards.** The guard names the entity's natural key — `name` for `Role`, `Duty`, `Branch`, `OutcomeState`, `AssignmentStatus`, `PermissionTier`, `MemberStatus`, `AttributeType`, `Capability`, `ServiceType`, `TimeOfDay`; `(name, service_type_id)` for `ServiceDefinition`; `(name, surname)` for `Member`. Where no natural key exists, the guard is a tuple that uniquely identifies the intended row.

**Lowest-id joins.** Where a foreign key must be resolved by name, the join is pinned to the lowest-id matching row: `ORDER BY id LIMIT 1`. This prevents duplicates in a lookup table from multiplying the seeded rows.

**No `ON CONFLICT DO NOTHING` except on primary keys.** `system_setting` uses `ON CONFLICT DO NOTHING` because its key is a true primary key. Every other table uses a `NOT EXISTS` guard, because names are not unique in the schema.

**Deterministic row identities.** Where a script needs to reference a row it inserted, it does so by re-querying on the natural key rather than by assuming an identity value.

---

## What the seed scripts are not

**Not authoritative.** A seed script that disagrees with the specification is wrong. The specification wins.

**Not a migration.** Seed scripts do not alter existing rows. They insert rows that do not exist. A database that already carries a row is left unchanged.

**Not production data.** The scripts populate a development or test environment. Production data is entered by the administrators.

**Not a substitute for configuration.** The seed data represents one possible configuration. The system is not required to run with this configuration; the admin can configure any configuration the specification permits.

**Not illustrative of MKN's actual configuration.** The MKN Configuration Reference (in `docs/spec/`) describes what MKN has configured. The seed data is a development convenience, not a reflection of any real congregation.

---

## What the seed scripts must not do

- **Not reference pre-amendment columns.** No `Member.IsActive`, no `Event.Location`, no `EventDuty.Label`.
- **Not reference pre-amendment settings.** No `TenureThresholdDays`, no `AgeRangeMin`, no `AgeRangeMax`.
- **Not depend on execution order** beyond what the foreign-key dependencies require.
- **Not fail on an empty database.** Every insert must succeed against a database created by `dotnet ef database update`.
- **Not assume a specific identity for the default rows.** The rewrite queries the default by its flag, not by an assumed primary key.

---

## Verification

After the rewrite, each script is applied to a freshly migrated database. The verification checks:

1. The script succeeds with no error.
2. The script succeeds again with no error and inserts no new rows.
3. Every foreign key in every inserted row resolves.
4. Every `SystemSetting` key the processes require is present.
5. The default `Role`, default `MemberStatus`, and default `PermissionTier` are identifiable by their flags.

The scripts are treated as tested only when all five checks pass.

---

*Source: System Design Specification §4, §15; the current seed scripts; the migration ordering.*
