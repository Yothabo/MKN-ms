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

Do not read `configuration_missing` as a permission failure. It is the required-setting rule (§2 of the specification): a process that depends on a required setting refuses to run when the setting is absent.

---

*Source: the operator-interface report; the frozen specification at `docs/spec/system-design-spec.md`; `docs/database/errors.md`. Subordinate to the specification.*
