# MKN Configuration Reference

*What an MKN administrator would enter into the system's configuration layer. Not part of the system's design — see the System Design Specification for the rule-neutral architecture this data is entered into.*

A note on how this document handles uncertainty: engineering questions (how the system should behave) are resolved with a definitive decision — that's design work. Questions about what MKN actually practices are a different kind of thing entirely — a real fact about a real organization that only MKN can confirm. For those, this document states a deliberately safe default: a configuration that lets the system function correctly and conservatively until the real answer is confirmed, never a guess presented as fact. Each is marked **(default — confirm with MKN)**.

---

## Roles configured

Evangelist, Messenger, Facilitator, Clerk, Conciliator, Steward, Songster.

**(default — confirm with MKN)** Clerk, Conciliator, Steward, and Songster carry no Duty Rule entries and no Permission Tier assignment. This is simply the correct inert state for an unconfigured role — not a gap that blocks anything — until MKN specifies what, if anything, these roles are responsible for.

## Duties configured

Chair, Messenger duty, Facilitator duty, Evangelist duty, Announcements, Theme Reader.

**(default — confirm with MKN)** "Theme Reader" is retained as the working name. The more precise term offered was "Obala inhloko zendima," with no agreed English rendering yet. The system functions identically regardless of the label; renaming it later is a one-field change.

## Duty Rule entries configured

| Duty | Configured tiers |
| --- | --- |
| Chair | Tier 1: Age Range 13–35. Tier 2: *(default — confirm with MKN, see below)*. Tier 3: no criteria (any eligible member). |
| Messenger duty | Tier 1: Role = Messenger. No further tier. |
| Facilitator duty | Tier 1: Gender = Female. Tier 2: Gender = Male. Tier 3: no criteria (any eligible Facilitator). |
| Evangelist duty | Tier 1: Role = Evangelist. No further tier. |
| Announcements | Tier 1: Role = Evangelist. Tier 2: *(default — confirm with MKN, see below)*. |
| Theme Reader | No tiers configured — open to all members. |

**(default — confirm with MKN) Chair, Tier 2:** an unconfirmed report suggested Evangelist or Facilitator may now also be included at this tier, where previously the duty may have been youth-only. Configured default: **do not add this tier yet.** Adding a tier based on an unconfirmed report risks assigning someone to a duty the congregation hasn't actually authorized them for; leaving the tier out is the reversible, conservative choice — it can be added the moment it's confirmed, at no cost, whereas wrongly adding it and later retracting it is a more visible correction.

**(default — confirm with MKN) Announcements, Tier 2:** confirmed for Short/Healing services; unconfirmed whether it extends to Full services. Configured default: **scope the Facilitator fallback to Short/Healing services only**, via Service Occurrence Duty / Service Definition Duty distinctions between the two service types. Same reasoning as above — don't extend a confirmed rule beyond its confirmed scope.

## Slot counts configured

Messenger duty and Facilitator duty: administrator sets a count per service (no fixed number assumed by the system). All other configured duties: single slot.

## Eligibility entries configured

Chair, for the Wednesday service specifically, requires an Eligibility flag granted by a Clerk, tied to a purity determination the Clerk already makes as part of their existing role. No personal status is stored directly — only the grant/revoke record.

## Service configuration

Short/Healing service type requires only Chair, Messenger duty, and Announcements. Full service type requires the complete duty set.

Wednesday's Branch Time Slot start time changes between school term and school holidays. **(default — confirm with MKN)** Configured as a manual edit to that Time Slot's StartTime, performed by an administrator each time the term changes — roughly twice a year. No automated school-calendar logic is configured, since school holiday timing varies by country and building that logic is disproportionate to a twice-yearly manual edit.

## Membership and branch context

**(default — confirm with MKN) Receipt-to-card issuance duration:** described as roughly three months. Configured as `System Setting: ReceiptToCardDurationDays = 90`, same reasoning as above.

**(default — confirm with MKN) Pre-uniform membership stage duty scope:** described as limited to Theme Reader, with no confirmation that this is the complete scope. Configured default: **Theme Reader only** — the described, not assumed, scope. Expanding it requires a deliberate confirmed addition, not an assumption now.

**(default — confirm with MKN) Identifier reassignment policy for death or leaving the congregation:** no described policy existed. Configured default: **identifiers are never reused.** A vacated identifier is marked with an UnassignedDate and Reason in Identifier History and permanently retired, never reassigned to a different member. This is the safer default regardless of what MKN eventually decides — it avoids any possibility of two people's history becoming entangled under one identifier, and relaxing it later (if MKN explicitly wants reuse under specific circumstances) is a straightforward, additive change; the reverse — un-entangling two people's merged history — is not.

## Branch-visitor duty eligibility

**(default — confirm with MKN)** No described policy existed for whether a visiting member (attending a branch other than their registered one) can be assigned duties there. Configured default: **open — not restricted to home branch.** This follows directly from the already-confirmed decision that attendance and RA status are computed across all branches, not just a member's home branch (a visiting member is not "absent"); restricting duty eligibility by branch while treating attendance as branch-agnostic would be an inconsistent pair of rules. If MKN wants tighter control later, Branch-Attendance Recency is already available as a Duty Rule criteria type to express that without reversing this default.

## Manual assignment and trust signals

**(default — confirm with MKN)** No described policy existed for whether a manually-created Roster Assignment should count toward future trust-based Priority signals (Acceptance Rate, Duties Carried) once those are usable. Configured default: **excluded.** This is the logically consistent choice, not an arbitrary pick — AssignmentSource exists specifically to distinguish an administrator's manual override from the engine's own candidate selection, and counting an override as if it were an earned pattern would corrupt the exact signal that distinction was built to protect.

## Occurrence materialization

`System Setting: OccurrenceHorizonDays` — **(default — confirm with MKN)** no org-specific preference described. Configured default: **30.** A month of rolling visibility is enough for an administrator to see and override upcoming occurrences well ahead of time, without materializing further into the future than is useful. This is a required setting per the System Design Specification — the Materializer will refuse to run and surface a configuration error if this is ever unset, rather than silently doing nothing.

## Thursday service configuration

**(default — confirm with MKN)** Thursday's duty rules (a women's duty, and a separate open-to-anyone duty) were never described in enough detail to configure. Configured default: **no Service Definition or Schedule exists for Thursday.** This is simply the correct inert state for an unconfigured service, consistent with how every other unconfigured part of this system behaves — not a blocker, and not part of the current rostering scope (Wednesday, Friday, Saturday, Sunday).
