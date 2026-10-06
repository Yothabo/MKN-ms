-- MKN-MS development seed data.
-- Minimal configuration to exercise the configuration layer and process 11.0.
--
-- Idempotent: safe to re-run. Every insert is guarded by a NOT EXISTS
-- check on the entity's natural key. ON CONFLICT DO NOTHING is used only
-- where a real uniqueness constraint exists (system_setting.key is the
-- primary key). It is not used elsewhere, because names are not unique
-- in this schema — the guard would not fire and duplicates would accumulate.
--
-- All lookup joins are pinned to the lowest-id matching row, so a
-- pre-existing duplicate in a lookup table does not multiply the rows
-- this script inserts.

-- ---------------------------------------------------------------------
-- system_setting
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('OccurrenceHorizonDays', '30', TRUE, 'Rolling horizon in days for occurrence materialization')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D10 lookups
-- ---------------------------------------------------------------------
INSERT INTO time_of_day (name)
SELECT 'Morning'
WHERE NOT EXISTS (SELECT 1 FROM time_of_day WHERE name = 'Morning');

INSERT INTO time_of_day (name)
SELECT 'Evening'
WHERE NOT EXISTS (SELECT 1 FROM time_of_day WHERE name = 'Evening');

INSERT INTO service_type (name)
SELECT 'Regular'
WHERE NOT EXISTS (SELECT 1 FROM service_type WHERE name = 'Regular');

INSERT INTO service_type (name)
SELECT 'Special'
WHERE NOT EXISTS (SELECT 1 FROM service_type WHERE name = 'Special');

INSERT INTO outcome_state (name)
SELECT 'Unfilled'
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Unfilled');

INSERT INTO outcome_state (name)
SELECT 'Partially Filled'
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Partially Filled');

INSERT INTO outcome_state (name)
SELECT 'Filled'
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Filled');

INSERT INTO assignment_status (name, is_terminal)
SELECT 'Proposed', FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Proposed');

INSERT INTO assignment_status (name, is_terminal)
SELECT 'Confirmed', FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Confirmed');

INSERT INTO assignment_status (name, is_terminal)
SELECT 'Declined', TRUE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Declined');

INSERT INTO assignment_status (name, is_terminal)
SELECT 'Timed Out', TRUE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Timed Out');

INSERT INTO permission_tier (name)
SELECT 'Full Admin'
WHERE NOT EXISTS (SELECT 1 FROM permission_tier WHERE name = 'Full Admin');

INSERT INTO permission_tier (name)
SELECT 'Roster Admin'
WHERE NOT EXISTS (SELECT 1 FROM permission_tier WHERE name = 'Roster Admin');

-- ---------------------------------------------------------------------
-- D1 Role and Duty
-- ---------------------------------------------------------------------
INSERT INTO role (name, is_active)
SELECT 'Coordinator', TRUE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE name = 'Coordinator');

INSERT INTO role (name, is_active)
SELECT 'Steward', TRUE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE name = 'Steward');

INSERT INTO duty (name, is_active)
SELECT 'Welcome duty', TRUE
WHERE NOT EXISTS (SELECT 1 FROM duty WHERE name = 'Welcome duty');

INSERT INTO duty (name, is_active)
SELECT 'Reading duty', TRUE
WHERE NOT EXISTS (SELECT 1 FROM duty WHERE name = 'Reading duty');

-- ---------------------------------------------------------------------
-- D3 Branch
-- ---------------------------------------------------------------------
INSERT INTO branch (name, location, is_active)
SELECT 'Main', 'Main Street', TRUE
WHERE NOT EXISTS (SELECT 1 FROM branch WHERE name = 'Main');

-- ---------------------------------------------------------------------
-- D3 Branch Time Slot
-- Pinned to the lowest-id Main branch and lowest-id Morning lookup.
-- ---------------------------------------------------------------------
INSERT INTO branch_time_slot (branch_id, day_of_week, time_of_day_id, is_active)
SELECT b.branch_id, 'Sunday', t.time_of_day_id, TRUE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT time_of_day_id FROM time_of_day WHERE name = 'Morning' ORDER BY time_of_day_id LIMIT 1) t
WHERE NOT EXISTS (
    SELECT 1 FROM branch_time_slot bts
    WHERE bts.branch_id = b.branch_id
      AND bts.day_of_week = 'Sunday'
      AND bts.time_of_day_id = t.time_of_day_id
);

-- ---------------------------------------------------------------------
-- D3 Service Definition (Regular)
-- Preserves the Special service definition if present; never touches it.
-- ---------------------------------------------------------------------
INSERT INTO service_definition (name, service_type_id, owning_branch_id, is_active)
SELECT 'Sunday Service', st.service_type_id, NULL, TRUE
FROM (SELECT service_type_id FROM service_type WHERE name = 'Regular' ORDER BY service_type_id LIMIT 1) st
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition sd
    WHERE sd.name = 'Sunday Service'
      AND sd.service_type_id = st.service_type_id
);

-- ---------------------------------------------------------------------
-- D3 Service Schedule
-- Pinned to the lowest-id Regular Sunday Service and lowest-id Main
-- Sunday Morning time slot.
-- ---------------------------------------------------------------------
INSERT INTO service_schedule (service_def_id, time_slot_id, start_time, is_active)
SELECT sd.service_def_id, bts.time_slot_id, '09:00', TRUE
FROM (SELECT service_def_id FROM service_definition WHERE name = 'Sunday Service' ORDER BY service_def_id LIMIT 1) sd,
     (SELECT bts.time_slot_id FROM branch_time_slot bts
        JOIN branch b ON b.branch_id = bts.branch_id
        JOIN time_of_day t ON t.time_of_day_id = bts.time_of_day_id
       WHERE b.name = 'Main'
         AND bts.day_of_week = 'Sunday'
         AND t.name = 'Morning'
       ORDER BY bts.time_slot_id LIMIT 1) bts
WHERE NOT EXISTS (
    SELECT 1 FROM service_schedule ss
    WHERE ss.service_def_id = sd.service_def_id
      AND ss.time_slot_id = bts.time_slot_id
      AND ss.is_active = TRUE
);

-- ---------------------------------------------------------------------
-- D2 Duty Rule — Welcome duty, Tier 1, Role = Coordinator
-- ---------------------------------------------------------------------
INSERT INTO duty_rule (duty_id, tier_order, criteria_type, criteria_value, is_active)
SELECT d.duty_id, 1, 'Role', 'Coordinator', TRUE
FROM (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM duty_rule dr
    WHERE dr.duty_id = d.duty_id
      AND dr.tier_order = 1
      AND dr.criteria_type = 'Role'
      AND dr.criteria_value = 'Coordinator'
);

-- ---------------------------------------------------------------------
-- D4 Member — Alex Example, pinned to lowest-id Main branch and
-- lowest-id Coordinator role.
-- ---------------------------------------------------------------------
INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage, gender, phone, email, branch_id, role_id, is_active)
SELECT 'Alex', 'Example', '2024-01-15', '1990-05-20', 'Full', 'Other', '+27-000-0000', 'alex@example.com', b.branch_id, r.role_id, TRUE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Coordinator' ORDER BY role_id LIMIT 1) r
WHERE NOT EXISTS (
    SELECT 1 FROM member m
    WHERE m.name = 'Alex' AND m.surname = 'Example'
);

-- ---------------------------------------------------------------------
-- D11 settings consumed by 7.0 Manage Confirmation.
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('InitialAssignmentStatusID', '2', TRUE,
        'The AssignmentStatus a new assignment transitions to on confirmation')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('ConfirmationTimeoutHours', '48', TRUE,
        'Hours after which an unresponded assignment is considered timed out')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D11 settings consumed by 10.0 Evaluate Fill Status.
-- Ids map to the OutcomeState rows seeded above, in seed order:
-- 1 = Unfilled, 2 = Partially Filled, 3 = Filled.
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('OutcomeStateUnfilledID', '1', TRUE,
        'OutcomeState to write when an occurrence is mechanically Unfilled')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('OutcomeStatePartiallyFilledID', '2', TRUE,
        'OutcomeState to write when an occurrence is mechanically Partially Filled')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('OutcomeStateFilledID', '3', TRUE,
        'OutcomeState to write when an occurrence is mechanically Filled')
ON CONFLICT (key) DO NOTHING;

-- OutcomeStateCancelledID is optional per §12.1.4. Left absent so
-- that the default behaviour (no automatic cancellation exclusion)
-- applies to mkn_dev until an administrator configures it.

-- ---------------------------------------------------------------------
-- D11 settings consumed by 9.0 Dispatch Notification.
-- NotificationChannel names the transport 9.0 dispatches through.
-- §14.1.2 places the actual transport outside the schema; the setting
-- here names which transport the process should use. "Log" is the
-- default when the setting is absent, but writing it explicitly means
-- mkn_dev exercises the read path rather than the fallback.
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('NotificationChannel', 'Log', FALSE,
        'The channel 9.0 dispatches assignment notices through')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D11 settings consumed by all processes that derive a calendar date.
-- §7 requires occurrence dates to be generated using the configured
-- system/application timezone, not UTC. The setting holds an IANA
-- timezone ID. It is not listed among the required settings in §15;
-- the resolver falls back to UTC if it is absent.
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('ApplicationTimeZone', 'Africa/Johannesburg', FALSE,
        'IANA timezone ID used to derive operational calendar dates')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D11 settings consumed by 7.0 Manage Confirmation, per §11.1.7.
-- These complete §15.3's list: §11.1.7 names "the admin-designated
-- declined status" and "the admin-designated timed-out status," and
-- §15's preamble states that it gathers every setting the locked
-- contracts imply. The settings are implied; the list is completed
-- here.
--
-- Ids map to the AssignmentStatus rows seeded above, in seed order:
-- 1 = Proposed, 2 = Confirmed, 3 = Declined, 4 = Timed Out.
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('DeclinedStatusID', '3', TRUE,
        'The AssignmentStatus a declined assignment transitions to')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('TimedOutStatusID', '4', TRUE,
        'The AssignmentStatus a timed-out assignment transitions to')
ON CONFLICT (key) DO NOTHING;
