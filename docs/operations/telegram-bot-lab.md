# Telegram Bot Lab

*The operator-facing lab note for the interim Telegram client. Not a specification document. Where this note conflicts with the frozen specification, the specification wins.*

---

## 1. What the bot is

The Telegram bot is an interim operator client for the MKN-MS HTTP API. It drives the existing endpoints directly. Every command is one HTTP call. It holds no state, has no local database, runs no scheduled jobs, and caches nothing. If the bot process restarts, nothing is lost because nothing is held.

The bot exists to prove the API's operator ergonomics before a web UI locks in assumptions. It is disposable. When the web UI exists, the bot may be deleted or kept as an admin side-channel. It is not the product.

---

## 2. What it is not

**The bot allowlist is not API authentication. The API trusts any caller that can reach it. Run the bot and the API on a private network, and do not expose the API elsewhere.**

The bot is not an authorization layer. It does not enforce the capability matrix. It is not suitable for public use. It is not a substitute for the eventual web UI.

The API's lack of authentication is deferred by the frozen specification (§4 Admin, C6 in `docs/processes/contracts.md`). The bot does not close that deferral. Its allowlist is a client-side filter that stops an unrelated Telegram user from driving the bot. It does not prove the caller is the admin named in the map.

---

## 3. The command set

Each command is one HTTP call to an existing endpoint. The bot renders the response or the error faithfully. It does not retry and does not transform error codes.

The stub in the first commit ships one command:

    /ping           GET /

Commands added later, in order of need:

**Vocabulary and membership**

    /roles_create <name>                       POST /api/config/1.0/roles
    /roles_list                                GET  /api/config/1.0/roles
    /duties_create <name>                      POST /api/config/1.0/duties
    /branches_list                             GET  /api/config/1.0/branches
    /members_create <...ten fields...>         POST /api/config/3.0/members
    /members_list [branchId]                   GET  /api/config/3.0/members
    /members_set_status <memberId> <statusId>  POST /api/config/3.0/members/{id}/set-status

**Eligibility and duty rules**

    /eligibility_grant <memberId> <dutyId> <grantedDate>   POST /api/config/4.0/members/{memberId}/eligibility
    /duty_rules_create <dutyId> <tierOrder> <criteriaType> <criteriaValue>   POST /api/config/2.0/duties/{dutyId}/rules

**Roster and attendance**

    /roster_generate <occurrenceId>            POST /api/processes/5.0/generate
    /attendance_record <memberId> <occurrenceId>   POST /api/processes/6.0/record

The full route inventory is in `docs/api/adapter-contract.md`.

---

## 4. The AdminId map

The bot's only client-side policy is an allowlist that maps Telegram user IDs to seeded AdminId values. It is set in `.env` on the device and never committed:

    MKN_ADMIN_MAP=123456789:1,987654321:2

Left side is the Telegram user ID; right side is an AdminId that exists in `admin` table. When a command arrives, the bot extracts the sender's Telegram user ID, looks it up, and uses the mapped AdminId as the acting admin for that call.

The map is administrative, not a security assertion. It is not authentication. It is what makes the bot usable without inventing an auth mechanism the specification has deferred.

---

## 5. What to watch for

The bot renders errors using the codes in `docs/database/errors.md`. Common ones an operator will encounter:

- `configuration_missing` — a required SystemSetting is not configured. Seed it, then retry.
- `configuration_invalid` — a required setting exists but its value is unparseable.
- `validation_failed` — the request's shape is wrong. The detail names the field.
- `not_found` — a referenced entity does not exist.
- `duplicate_name` — the entity already exists. Update instead of create.
- `referenced_entity_missing` — the referenced entity was never created. Create it first.
- `referenced_entity_inactive` — the referenced entity exists but is inactive or soft-deleted.
- `lifecycle_violation` — the operation is not permitted from the entity's current state. Often a cascade block.

A `500` from the API is a server fault. Check the API log.

**Reserved criteria are inert.** The criteria types `AcceptanceRate`,
`DutiesCarried`, and `DaysSinceLastAssignment` are in the write-time vocabulary but
are not evaluated. A duty rule using one of them writes successfully and has no
effect on candidate selection. Do not rely on them; the specification reserves them
for a future revision that will define their evaluation. All other criteria types
(Role, Gender, AgeRange, Tenure, MembershipStage, BranchAttendanceRecency,
EligibilityFlag, MemberAttribute, MemberStatus, Youth) are evaluated.

**Identifier workflow.** Attendance-register eligibility (§13.1.1) reads
`Member.CardNumber` or `Member.ReceiptNumber`. Two operations touch those
columns and they are not the same:

  - `/member_set_identifier <memberId>|<card>|<receipt>` — sets the current
    value on the member. Use `-` for either slot to leave it unchanged. This
    is what attendance reads.
  - `/identifier_issue <memberId>|<type>|<number>|<assignedDate>` — writes
    an IdentifierHistory row. A historical log entry. Does not set the
    member columns.

A member who has only had an identifier issued (history) but has not had
`/member_set_identifier` run against them cannot attend. Both operations
are meaningful independently — the history log records that an identifier
was issued or retired; the member columns record the current value — so
they remain separate. The common lab workflow runs both in order.

**Duplicate name submissions.** Every vocabulary entity — Role, Duty, Branch, PermissionTier, MemberStatus, TimeOfDay, ServiceType, OutcomeState, AssignmentStatus, Capability, AttributeType — enforces case-insensitive name uniqueness among live rows. ServiceDefinition enforces the same, scoped by OwningBranchId. When the operator submits a duplicate name, the API returns `duplicate_name` / `Conflict`, and the bot renders it as `Already exists: ...`. The system does not silently create a second row. Member is exempt from this rule; two members may share a name and a date of birth, and the operator resolves the conflict by setting the unwanted member's status to a non-rosterable status (member rows are never deleted). The amendment is §15.8.40; the constraint is enforced by the database, not by the API or the bot.

Do not read `configuration_missing` as a permission failure. It is the required-setting rule (§2 of the specification): a process that depends on a required setting refuses to run when the setting is absent.

---

## 6. Lab result — closed

The lab's success criteria (the operator-interface report's §8) are met. From a
database with only the bootstrap rows, using only the Telegram bot, an operator
can reach a recorded attendance record without touching psql:

    /roles_create
    /duties_create
    /branches_create
    /members_create
    /duty_rules_create
    /eligibility_grant
    /service_defs_create
    /service_def_schedules_create
    /materialize_manual
    /roster_generate
    /member_set_identifier
    /attendance_record

Each of the five criteria:

1. **An operator can take the system from skeletal to working for the happy path.**
   Proven by the twelve-command chain above, run against the live API.

2. **Failures return understandable messages.** `configuration_missing`,
   `duplicate_name`, `not_found`, `validation_failed`, `lifecycle_violation`, and
   process-side `errorDetail` all render as short operator-language replies. The
   `postJson` cap at 300 characters prevents a server error from exceeding
   Telegram's reply limit.

3. **A second mapped admin is visible on at least one audited or approved action.**
   The AdminId map is exercised; the "second admin" case is not (only one admin
   exists in the lab environment). Recorded as a deferred observation, not a
   defect.

4. **A written task list of the five to eight flows actually used exists.** The
   twelve-command chain above is that list.

5. **API remains non-public.** The API binds `127.0.0.1:5020`, never `0.0.0.0`.
   No incident of treating the bot allowlist as an auth boundary. The lab note's
   first sentence in §2 remains the operating rule.

## 7. Lab findings

The lab surfaced two real gaps in the frozen specification. Both were closed
by amendment, in the same session, before the lab's verdict was written.

- **§15.8.40 — vocabulary name uniqueness.** An operator's `/duties_create
  Welcome duty` run against a database that already held a "Welcome duty" row
  produced a second row with the same name. Sessions produced duplicate roles,
  duties, and members. The specification's §9.1.2 said "names are required but
  not unique." The lab made that position untenable. The amendment enforces
  case-insensitive uniqueness among live rows at the database level for every
  vocabulary entity, and surfaces violations as `duplicate_name` / `Conflict`.
  Member is deliberately exempt: real members can share names and birthdates.

- **§15.8.41 — member identifier update surface.** `RecordAttendanceService`
  requires `Member.CardNumber` or `Member.ReceiptNumber` (§13.1.1). Neither
  `CreateMemberRequest` nor `UpdateMemberRequest` includes those two fields,
  and `IdentifierHistory` writes the log, not the member row. The seed scripts
  set them by SQL. An operator driving the API could not, so attendance was
  unreachable for any member the operator created. The amendment adds
  `POST /api/config/3.0/members/{id}/set-identifier`.

Three observations that are not defects, recorded so they do not disappear:

- **Reserved criteria are inert.** `AcceptanceRate`, `DutiesCarried`, and
  `DaysSinceLastAssignment` are in the write-time vocabulary but not evaluated.
  Recorded in §5 above.

- **Two bootstrap prerequisites have no HTTP surface.** `EntityDeletionPolicy`
  rows and `MemberStatus` creation must be seeded or written by SQL. Neither
  has an endpoint. Recorded in the operator runbook; the lab does not close
  them.

- **The scheduler runs through every session restart.** Occurrences, fill-status
  classifications, and timeout transitions are created by the scheduled jobs
  across restarts, not only by operator commands. This is the deployed-system
  experience and is correct.

## 8. What remains

Nothing blocking. The following are queued and are **incremental lab work**,
not close-out:

- Three process commands: `/confirmation_sweep` (7.0), `/fill_evaluate` (10.0),
  `/attendance_rule_manual` (13.0). These exercise the confirmation lifecycle,
  fill-status classification, and the rule engine.
- The reserved fairness criteria — activating `DaysSinceLastAssignment` or
  `DutiesCarried` as evaluated criteria is a future specification amendment,
  not a bot command.
- The deferred items in `docs/operations/conformance.md` — authentication,
  authorization, tap ingestion, notification templates, per-member channel
  preferences, health endpoint. None is the lab's concern.

The lab is closed. The bot is proven against the surface it was built to
exercise. The two gaps it found are closed by amendment. The record above is
the artifact; the twelve-command chain is the proof.

*Source: the operator-interface report; the frozen specification at `docs/spec/system-design-spec.md`; `docs/database/errors.md`. Subordinate to the specification.*
