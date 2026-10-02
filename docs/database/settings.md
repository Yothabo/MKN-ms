# Settings

*Every SystemSetting key the specification defines. Derived from the System Design Specification §15. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

SystemSetting is a generic key/value store for scalar, non-list configuration values. The existence of each key is fixed by the specification — the system must know that a setting named X is a thing it consults. The value is entirely administrator-configured, with no built-in default.

This document lists every setting the specification defines, its type, whether it is required, and which process consumes it.

---

## The settings

### OccurrenceHorizonDays

| Property | Value |
| --- | --- |
| Key | OccurrenceHorizonDays |
| Type | integer |
| Unit | days |
| Minimum | 0 |
| Required | true |
| Consumed by | 11.0 Materialize Occurrences |
| Behavior if absent | 11.0 refuses to run and surfaces a configuration error |
| Source | §7, §8, §15.3.1 |

Rolling horizon in days for occurrence materialization. 0 means materialize today only.

### InitialAssignmentStatusID

| Property | Value |
| --- | --- |
| Key | InitialAssignmentStatusID |
| Type | integer — an AssignmentStatusID |
| Required | true |
| Consumed by | 7.0 Manage Confirmation |
| Behavior if absent | 7.0 refuses to run and surfaces a configuration error |
| Source | §11, §15.3.2 |

Identifies the status to which a new assignment transitions from NULL.

### ConfirmationTimeoutHours

| Property | Value |
| --- | --- |
| Key | ConfirmationTimeoutHours |
| Type | integer |
| Unit | hours |
| Required | Configurable; specification does not fix the value |
| Consumed by | 7.0 Manage Confirmation |
| Behavior if absent | If Required = false, no timeout occurs; if Required = true, 7.0 refuses to run and surfaces a configuration error |
| Timeout rule | now - CreatedAt >= ConfirmationTimeoutHours |
| Source | §11, §15.3.3 |

### OutcomeStateUnfilledID

| Property | Value |
| --- | --- |
| Key | OutcomeStateUnfilledID |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 Evaluate Fill Status |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12, §15.3.4 |

Maps the Unfilled mechanical condition to an administrator-defined OutcomeState.

### OutcomeStatePartiallyFilledID

| Property | Value |
| --- | --- |
| Key | OutcomeStatePartiallyFilledID |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 Evaluate Fill Status |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12, §15.3.5 |

Maps the Partially Filled mechanical condition.

### OutcomeStateFilledID

| Property | Value |
| --- | --- |
| Key | OutcomeStateFilledID |
| Type | integer — an OutcomeStateID |
| Required | true |
| Consumed by | 10.0 Evaluate Fill Status |
| Behavior if absent | 10.0 refuses to run and surfaces a configuration error |
| Source | §12, §15.3.6 |

Maps the Filled mechanical condition.

### OutcomeStateCancelledID

| Property | Value |
| --- | --- |
| Key | OutcomeStateCancelledID |
| Type | integer — an OutcomeStateID |
| Required | false |
| Consumed by | 10.0 Evaluate Fill Status |
| Behavior if absent | No automatic cancellation exclusion applies; 10.0 evaluates every occurrence |
| Behavior if present | 10.0 skips any occurrence whose current FillStatusID equals that ID |
| Note | 10.0 never writes the cancelled state |
| Source | §12 E2 lock, §15.3.7 |

### NotificationChannel

| Property | Value |
| --- | --- |
| Key | NotificationChannel |
| Type | string or enum — the channel name |
| Consumed by | 9.0 Dispatch Notification |
| Validation | If no valid channel is configured, 9.0 does not send. No channel-specific fallback is defined by the 9.0 contract. Whether the setting is configured as required is a deployment choice, not fixed by the specification. |
| Source | §14, §15.3.8 |

---

## Existing settings referenced but not new

The following settings are referenced by the locked contracts but were described in the MKN Configuration Reference and in §4 of the specification, not introduced by the amendment sweep. They are listed here for completeness.

### TenureThresholdDays

| Property | Value |
| --- | --- |
| Key | TenureThresholdDays |
| Type | integer |
| Unit | days |
| Consumed by | 5.0 Generate Assignment, when a Tenure criterion is evaluated |
| Source | MKN Configuration Reference |

### ReceiptToCardDurationDays

| Property | Value |
| --- | --- |
| Key | ReceiptToCardDurationDays |
| Type | integer |
| Unit | days |
| Consumed by | Administrative process (receipt-to-card issuance) |
| Source | MKN Configuration Reference |

### Age-range bounds

| Property | Value |
| --- | --- |
| Key(s) | AgeRangeMin, AgeRangeMax (or equivalent) |
| Type | integer |
| Consumed by | 5.0 Generate Assignment, when an Age Range criterion is evaluated |
| Source | MKN Configuration Reference |

---

## The complete list

| Key | Type | Required | Consumer |
| --- | --- | --- | --- |
| OccurrenceHorizonDays | integer | true | 11.0 |
| InitialAssignmentStatusID | integer | true | 7.0 |
| ConfirmationTimeoutHours | integer | configurable | 7.0 |
| OutcomeStateUnfilledID | integer | true | 10.0 |
| OutcomeStatePartiallyFilledID | integer | true | 10.0 |
| OutcomeStateFilledID | integer | true | 10.0 |
| OutcomeStateCancelledID | integer | false | 10.0 |
| NotificationChannel | string | deployment choice | 9.0 |
| TenureThresholdDays | integer | not fixed | 5.0 (Tenure criterion) |
| ReceiptToCardDurationDays | integer | not fixed | Administrative |
| Age-range bounds | integer | not fixed | 5.0 (Age Range criterion) |

---

## The required-setting rule

The `Required` boolean governs behavior when a value is absent:

| Required | Value present | Behavior |
| --- | --- | --- |
| true | yes | Process runs normally |
| true | no | Process refuses to run, surfaces a configuration error |
| false | yes | Process uses the value |
| false | no | Process has a defined behavior for the value being absent |

**Absent does not mean "use a default."** The specification asserts no implicit defaults anywhere. Absent means the value is genuinely missing, and each process defines what it does in that case.

For all processes in the current specification, a required setting that is missing causes the process to refuse to run. This is the required-setting rule from §2 of the specification.

---

## Cross-references

- **Specification §2** — the required-setting rule.
- **Specification §7, §11, §12, §14** — the contract definitions for each setting's consumer.
- **Specification §15** — the amendment list.
- **`../processes/operations/11.0-materialize-occurrences.md`** — consumes OccurrenceHorizonDays.
- **`../processes/operations/7.0-manage-confirmation.md`** — consumes InitialAssignmentStatusID and ConfirmationTimeoutHours.
- **`../processes/operations/10.0-evaluate-fill-status.md`** — consumes the four OutcomeState settings.
- **`../processes/operations/9.0-dispatch-notification.md`** — consumes NotificationChannel.

---

*Source: System Design Specification §4, §7, §11, §12, §14, §15; MKN Configuration Reference.*
