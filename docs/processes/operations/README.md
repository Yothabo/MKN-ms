# Operations Layer

*The eight processes that produce operational records from configuration and stored facts. Derived from the System Design Specification §7–§17. Where this document conflicts with the specification, the specification wins.*

---

## Purpose

The operations layer is the set of processes that turn configuration and stored facts into operational outcomes: materialized occurrences, generated assignments, tracked confirmations, recorded attendance, evaluated fill status, dispatched notifications, and attendance-driven status changes.

These processes read configuration at run time. None of them embeds an organization's rules.

---

## The processes

| # | Process | Responsibility |
| --- | --- | --- |
| 5.0 | Generate Assignment | Fills the required duty slots of a Service Occurrence by creating Roster Assignments |
| 6.0 | Record Attendance | Records the raw fact of a member's presence at an occurrence |
| 7.0 | Manage Confirmation | Tracks each Roster Assignment's response lifecycle; re-resolves on decline or timeout |
| 9.0 | Dispatch Notification | Sends assignment notices to members and authority notifications to admins |
| 10.0 | Evaluate Fill Status | Computes each occurrence's fill state and writes FillStatusID |
| 11.0 | Materialize Occurrences | Generates Service Occurrences from active Service Schedules on a rolling horizon |
| 12.0 | Create Manual Assignment | Directly creates a Roster Assignment from an administrator's selection |
| 13.0 | Attendance Rule Engine | Reads Attendance Rules and applies configured outcomes |

---

## The layer boundary

**Reads:** configuration stores and operational stores, per each process's footprint.

**Writes:** operational stores (D3, D7, D8, D12), plus ConfigurationAuditLog.NotifiedAt written by 9.0 on its authority-notification path.

**Does not write:** configuration stores, except that 10.0 writes to D3 ServiceOccurrence.FillStatusID (which is an operational field on an operational record) and 9.0 writes ConfigurationAuditLog.NotifiedAt on Path 2.

---

## The invocation graph

The four invocation edges terminate at 9.0:

| # | From | To | Condition |
| --- | --- | --- | --- |
| I1 | 5.0 Generate Assignment | 9.0 Dispatch Notification | On each new automatic assignment created |
| I2 | 7.0 Manage Confirmation | 9.0 Dispatch Notification | On each replacement assignment created |
| I3 | 12.0 Create Manual Assignment | 9.0 Dispatch Notification | Only when AssignmentStatusID = NULL at creation |
| I4 | 13.0 Attendance Rule Engine | 9.0 Dispatch Notification | Only when an Attendance Rule's OutcomeType = Notify and its trigger fires |

Every other relationship between processes is data-mediated. 9.0 is the sole process any other process is permitted to invoke.

9.0 also carries a scheduled trigger that is not an invocation: the authority-notification sweep, which reads new ConfigurationAuditLog entries and dispatches notifications for them.

---

## Trigger summary

| Process | Trigger |
| --- | --- |
| 5.0 Generate Assignment | Scheduled run; manual administrator action |
| 6.0 Record Attendance | Member action; manual administrator entry |
| 7.0 Manage Confirmation | Member response; scheduled timeout check |
| 9.0 Dispatch Notification | Invocation by 5.0, 7.0, 12.0, or 13.0; scheduled authority-notification sweep |
| 10.0 Evaluate Fill Status | After assignment changes; scheduled sweep |
| 11.0 Materialize Occurrences | Scheduled run (daily); manual administrator action |
| 12.0 Create Manual Assignment | Administrator action |
| 13.0 Attendance Rule Engine | Scheduled run; manual administrator invocation |

---

## Store footprint

| Process | Reads | Writes |
| --- | --- | --- |
| 5.0 | D2, D3, D4, D6, D7, D8 (conditional) | D7 |
| 6.0 | D3, D4 | D8 |
| 7.0 | D2 (re-res), D3 (re-res), D4, D6 (re-res), D7, D8 (re-res), D10, D11 | D7 |
| 9.0 | Path 1: D3, D4, D7, D11. Path 2: ConfigurationAuditLog, NotificationSubscription, Admin, PermissionTier, D11. | ConfigurationAuditLog (NotifiedAt, Path 2) |
| 10.0 | D3, D7, D10, D11 | D3 |
| 11.0 | D3, D11 | D3, D12 |
| 12.0 | D2, D3, D4, D6, D7 | D7 |
| 13.0 | D2 (AttendanceRule), D3, D4, D5 (Readmission), D7 (AttendanceRecord), D10 (MemberStatus), D11 (scope) | D4 (MemberStatusID) |

---

## Universal invariants

- No process writes outside its footprint. Each process writes only to the stores it is contractually allowed to write to.
- No process invokes another except the four locked edges. 5.0 -> 9.0, 7.0 -> 9.0, 12.0 -> 9.0, and 13.0 -> 9.0.
- Required-setting discipline. Each process that depends on a required SystemSetting refuses to run if the setting is absent, and surfaces a configuration error.
- Lifecycle ownership is clean. 5.0 creates assignments with AssignmentStatusID = NULL; 7.0 owns the status lifecycle. 11.0 creates occurrences with FillStatusID = NULL; 10.0 owns the fill status lifecycle.
- Additivity. 5.0 is additive (never modifies or removes an existing assignment). 11.0 is additive and idempotent (never modifies or deletes an existing occurrence).

---

## Reading order

For a reader new to the operations layer:

1. This README — the layer overview.
2. 11.0-materialize-occurrences.md — the process that creates occurrences.
3. 5.0-generate-assignment.md — the process that fills them.
4. 7.0-manage-confirmation.md — the response lifecycle.
5. 10.0-evaluate-fill-status.md — the fill state evaluation.
6. 6.0-record-attendance.md — the raw presence fact.
7. 9.0-dispatch-notification.md — the outbound channel, both paths.
8. 12.0-create-manual-assignment.md — the direct admin override.
9. 13.0-attendance-rule-engine.md — the attendance rule engine.

---

*Source: System Design Specification §7–§17.*
