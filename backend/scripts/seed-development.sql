-- MKN-MS development seed data.
-- Minimal configuration to exercise the configuration layer and process 11.0.
-- Idempotent: safe to re-run. Existing rows are left untouched.

-- ---------------------------------------------------------------------
-- system_setting
-- ---------------------------------------------------------------------
INSERT INTO system_setting (key, value, required, description)
VALUES ('OccurrenceHorizonDays', '30', TRUE, 'Rolling horizon in days for occurrence materialization')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D10 lookups
-- ---------------------------------------------------------------------
INSERT INTO time_of_day (name) VALUES ('Morning') ON CONFLICT DO NOTHING;
INSERT INTO time_of_day (name) VALUES ('Evening') ON CONFLICT DO NOTHING;

INSERT INTO service_type (name) VALUES ('Regular') ON CONFLICT DO NOTHING;
INSERT INTO service_type (name) VALUES ('Special') ON CONFLICT DO NOTHING;

INSERT INTO outcome_state (name) VALUES ('Unfilled') ON CONFLICT DO NOTHING;
INSERT INTO outcome_state (name) VALUES ('Partially Filled') ON CONFLICT DO NOTHING;
INSERT INTO outcome_state (name) VALUES ('Filled') ON CONFLICT DO NOTHING;

INSERT INTO assignment_status (name, is_terminal) VALUES ('Proposed', FALSE) ON CONFLICT DO NOTHING;
INSERT INTO assignment_status (name, is_terminal) VALUES ('Confirmed', FALSE) ON CONFLICT DO NOTHING;
INSERT INTO assignment_status (name, is_terminal) VALUES ('Declined', TRUE) ON CONFLICT DO NOTHING;
INSERT INTO assignment_status (name, is_terminal) VALUES ('Timed Out', TRUE) ON CONFLICT DO NOTHING;

INSERT INTO permission_tier (name) VALUES ('Full Admin') ON CONFLICT DO NOTHING;
INSERT INTO permission_tier (name) VALUES ('Roster Admin') ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- D1 Role and Duty
-- ---------------------------------------------------------------------
INSERT INTO role (name, is_active) VALUES ('Coordinator', TRUE) ON CONFLICT DO NOTHING;
INSERT INTO role (name, is_active) VALUES ('Steward', TRUE) ON CONFLICT DO NOTHING;

INSERT INTO duty (name, is_active) VALUES ('Welcome duty', TRUE) ON CONFLICT DO NOTHING;
INSERT INTO duty (name, is_active) VALUES ('Reading duty', TRUE) ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- D3 Branch, Time Slot, Service Definition, Service Schedule
-- ---------------------------------------------------------------------
INSERT INTO branch (name, location, is_active) VALUES ('Main', 'Main Street', TRUE) ON CONFLICT DO NOTHING;

INSERT INTO branch_time_slot (branch_id, day_of_week, time_of_day_id, is_active)
SELECT b.branch_id, 'Sunday', t.time_of_day_id, TRUE
FROM branch b, time_of_day t
WHERE b.name = 'Main' AND t.name = 'Morning'
ON CONFLICT DO NOTHING;

INSERT INTO service_definition (name, service_type_id, owning_branch_id, is_active)
SELECT 'Sunday Service', st.service_type_id, NULL, TRUE
FROM service_type st
WHERE st.name = 'Regular'
ON CONFLICT DO NOTHING;

INSERT INTO service_schedule (service_def_id, time_slot_id, start_time, is_active)
SELECT sd.service_def_id, bts.time_slot_id, '09:00', TRUE
FROM service_definition sd, branch_time_slot bts, branch b, time_of_day t
WHERE sd.name = 'Sunday Service'
  AND bts.branch_id = b.branch_id
  AND bts.time_of_day_id = t.time_of_day_id
  AND b.name = 'Main'
  AND bts.day_of_week = 'Sunday'
  AND t.name = 'Morning'
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- D2 Duty Rule
-- ---------------------------------------------------------------------
INSERT INTO duty_rule (duty_id, tier_order, criteria_type, criteria_value, is_active)
SELECT d.duty_id, 1, 'Role', 'Coordinator', TRUE
FROM duty d
WHERE d.name = 'Welcome duty'
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------
-- D4 Member
-- ---------------------------------------------------------------------
INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage, gender, phone, email, branch_id, role_id, is_active)
SELECT 'Alex', 'Example', '2024-01-15', '1990-05-20', 'Full', 'Other', '+27-000-0000', 'alex@example.com', b.branch_id, r.role_id, TRUE
FROM branch b, role r
WHERE b.name = 'Main' AND r.name = 'Coordinator'
ON CONFLICT DO NOTHING;
