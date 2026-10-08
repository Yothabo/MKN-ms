# Notification Templates

---

## Named concern

Notification templates. The text a notification carries.

## Boundary

The boundary is inside 9.0 Dispatch Notification. The specification states that the message is composed and sent, and that the exact format is channel-dependent and outside the schema. The specification does not state where the message text comes from, whether the text is editable, whether templates are stored as entities, what happens when a template is missing, or whether template edits are audited.

The member-facing path composes its message. The authority-notification path composes its message from a `ConfigurationAuditLog` entry. Neither composition is defined by the specification.

## Status

Recognised, but not implementation-ready. The specification does not define the entities, the storage, or the API surface of templates. The concern is named; its content is open.

## Non-dependency

The thirteen defined processes do not depend on the content of notification templates. Every process can be implemented, tested, and run without the template content being decided. 9.0 composes a message; what the message says is not a concern the specification currently fixes.

---

*Source: System Design Specification §14.*
