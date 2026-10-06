-- MKN-MS development seed data.
-- Minimal configuration to exercise process 11.0 Materialize Occurrences.
-- Idempotent: safe to re-run. Existing rows are left untouched.

-- system_setting: the required horizon.
INSERT INTO system_setting (key, value, required, description)
VALUES ('OccurrenceHorizonDays', '30', TRUE, 'Rolling horizon in days for occurrence materialization')
ON CONFLICT (key) DO NOTHING;

-- Lookup: time_of_day.
INSERT INTO time_of_day (name)
VALUES ('Morning')
ON CONFLICT DO NOTHING;

-- Lookup: service_type.
INSERT INTO service_type (name)
VALUES ('Regular')
ON CONFLICT DO NOTHING;

-- Branch.
INSERT INTO branch (name, location, is_active)
VALUES ('Main', 'Main Street', TRUE)
ON CONFLICT DO NOTHING;

-- Branch time slot: Sunday morning at Main.
INSERT INTO branch_time_slot (branch_id, day_of_week, time_of_day_id, is_active)
SELECT b.branch_id, 'Sunday', t.time_of_day_id, TRUE
FROM branch b, time_of_day t
WHERE b.name = 'Main' AND t.name = 'Morning'
ON CONFLICT DO NOTHING;

-- Service definition: Sunday Service.
INSERT INTO service_definition (name, service_type_id, owning_branch_id, is_active)
SELECT 'Sunday Service', st.service_type_id, NULL, TRUE
FROM service_type st
WHERE st.name = 'Regular'
ON CONFLICT DO NOTHING;

-- Service schedule: Sunday Service at the Sunday morning slot, starting 09:00.
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
