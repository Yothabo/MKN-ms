# Operator Runbook

*What the operator needs to run, observe, and recover a deployed instance of MKN-MS. Not a specification document, not a derived design document. Where this runbook conflicts with the specification, the specification wins.*

---

## Required settings and their consumers

Every process that depends on a `SystemSetting` marked `Required = true` refuses to run and surfaces a configuration error when the value is absent. The rule is stated in the specification at §2 and applies uniformly.

| Setting | Consumer | Behavior when absent |
| --- | --- | --- |
| `OccurrenceHorizonDays` | 11.0 Materialize Occurrences | Refuses; `ConfigurationMissing` |
| `InitialAssignmentStatusID` | 7.0 Manage Confirmation (confirm) | Refuses; `ConfigurationMissing` |
| `DeclinedStatusID` | 7.0 Manage Confirmation (decline) | Refuses; `ConfigurationMissing` |
| `TimedOutStatusID` | 7.0 Manage Confirmation (sweep) | Refuses; `ConfigurationMissing` |
| `ConfirmationTimeoutHours` | 7.0 Manage Confirmation (sweep) | No timeout if `Required = false`; refuses if `Required = true` |
| `OutcomeStateUnfilledID` | 10.0 Evaluate Fill Status | Refuses the whole run |
| `OutcomeStatePartiallyFilledID` | 10.0 Evaluate Fill Status | Refuses the whole run |
| `OutcomeStateFilledID` | 10.0 Evaluate Fill Status | Refuses the whole run |
| `OutcomeStateCancelledID` | 10.0 Evaluate Fill Status | No exclusion applies |
| `NotificationChannel` | 9.0 Dispatch Notification | Both paths refuse |
| `ApplicationTimeZone` | 5.0, 7.0, 11.0, 12.0, 13.0, Quartz startup | Consuming process refuses; application refuses to start |
| `AttendanceRegisterEnabled` | 6.0, 13.0, 5.0's Recency criterion | Consuming process refuses |
| `YouthAgeMin`, `YouthAgeMax` | 5.0's Youth criterion | Criterion matches no candidate |

The rule is fail-loud, never default.

---

## Known operational risks

### Manual assignment bypasses `MemberStatus`

`CreateManualAssignmentService` (process 12.0) bypasses the non-EligibilityFlag Duty Rule criteria. It does not consult `MemberStatus.IsRosterable`; a member whose status is not rosterable can be manually assigned. This is deliberate — the admin's decision is the override.

**Operator implication:** a non-rosterable member on a roster was assigned manually, not automatically.

### Path 2 zero-recipient dispatch

When the authority-notification sweep finds an audit row whose matching `NotificationSubscription` rows resolve to zero recipients, the sweep marks the row notified and dispatches to no one. A `Warning` log entry naming the audit id is now produced per such row.

`ConfigurationAuditLog` does not record how many recipients a row's dispatch resolved to. The application-log warning is the only record of a zero-recipient row.

### ApplicationTimeZone restart caveat

The Quartz trigger timezones are resolved once at application startup. A change to `ApplicationTimeZone` affects the processes on their next invocation, but scheduled triggers keep firing on the previous zone's clock until the application restarts.

### Postgres stalls during test runs

Termux postgres is not production-grade. A full suite takes 18-25 minutes when healthy; 2+ hours when the postgres instance has stalled; the stall produces one to three failures whose only signal is `NpgsqlException: The operation has timed out`.

Operator procedure: run `pg-ensure`, re-run the failing class in isolation, and treat a green isolated run as proof the failure was a stall, not a defect.

### Test-side database creation requires `CREATEDB`

`TestDatabaseFixture` and `MigrationSchemaFixture` create and drop databases on demand. The user named in `MKN_TEST_ADMIN_CONNECTION` (or its fallback, `mkn_dev`) must hold `CREATEDB`. The CI runner uses `POSTGRES_USER: mkn_dev`, a superuser, which inherits it.

---

## Job-run observability

Of the six scheduled processes, only 11.0 persists its run record.

11.0 writes a `MaterializerRun` row per invocation. The other four — 7.0's timeout sweep, 10.0's fill-status sweep, 13.0's engine, 9.0's authority sweep — are visible only as application-log entries.

~~~sql
SELECT run_id, trigger_type, started_at, completed_at,
       schedules_evaluated, occurrences_created, status, error_detail
  FROM materializer_run
 ORDER BY run_id DESC
 LIMIT 20;
~~~

Log-line shapes to search for the other four:

- 7.0 sweep: `Scheduled confirmation timeout sweep completed:` / `... failed:`
- 10.0 sweep: `Scheduled fill-status sweep completed:` / `... failed:`
- 13.0 engine: `Scheduled attendance-rule run completed:` / `... failed:`
- 9.0 sweep: `Authority-notification sweep completed:` / `... failed:`

### Deferred: health / last-N endpoint

There is no HTTP endpoint reporting the last run of any scheduled process. A full-coverage endpoint requires a persisted job-run record for the four non-11.0 processes — a new entity the specification does not define. Adding the entity is a specification amendment, not an implementation choice.

The decision is deferred. A health endpoint covering only 11.0 is possible but implies coverage of the other four that does not exist; a health endpoint covering all five is the right shape and needs the amendment first.

Operator workaround: query `materializer_run` for 11.0; grep the application log for the four completion lines above.

---

## Reading the authority-notification audit

Every configuration deactivation and every soft delete writes a `ConfigurationAuditLog` row. `NotifiedAt` is set once the authority sweep processes the row. Rows with `NotifiedAt IS NULL` are pending.

~~~sql
SELECT audit_id, entity_type, entity_id, entity_name, action,
       initiated_at, reason
  FROM configuration_audit_log
 WHERE notified_at IS NULL
 ORDER BY audit_id ASC;
~~~

A row pending across many sweeps is the signal that dispatch is failing persistently; check the application log for `Authority notification failed for audit ...` entries.

---

## What the system will not do for the operator

- No delivery tracking. Neither notification path records whether a message was sent, when, through which channel, or whether it was delivered.
- No retry counter. Path 2 retries a row on every sweep while `NotifiedAt` is null, with no upper bound.
- No health endpoint for any scheduled process.
- No automatic recovery from a postgres stall during a fact's setup or body.
- No configuration-change notification to the operator beyond the audit log.

---

*Source: the specification at `../spec/system-design-spec.md` (§2, §9, §14.1, §15); the operational experience recorded in the project's handover record.*
