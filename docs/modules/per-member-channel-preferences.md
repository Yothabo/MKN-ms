# Per-Member Channel Preferences

---

## Named concern

Per-member channel preferences. Which channel a member prefers to receive notifications through.

## Boundary

The boundary is inside 9.0 Dispatch Notification. The specification states that the current scope assumes a single global channel and that per-member channel preferences are not modelled. The specification does not state whether a member carries a preferred channel, whether a separate entity stores preferences, or how a preference would interact with the global channel setting.

The `NotificationChannel` setting names the transport. The specification does not state what happens when a member's preference and the global setting differ.

## Status

Recognised, but not implementation-ready. The specification does not define the entities, the storage, or the API surface of per-member preferences. The concern is named; its content is open.

## Non-dependency

The thirteen defined processes do not depend on the content of per-member channel preferences. Every process can be implemented, tested, and run without the preference content being decided. 9.0 dispatches through the configured channel; whether a member prefers a different one is not a concern the current specification fixes.

---

*Source: System Design Specification §14.*
