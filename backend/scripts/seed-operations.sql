
-- MKN-MS development seed data: operational configuration.
--
-- Idempotent. Every insert is guarded by a NOT EXISTS check on the
-- entity's natural key. Lookup joins are pinned to the lowest-id
-- matching row.
--
-- Depends on seed-vocabulary.sql and seed-membership.sql.
--
-- Source: docs/database/seed-migration.md; spec §4, §5, §9.1.9,
-- §9.1.10, §9.1.11.

-- ---------------------------------------------------------------------
-- D2 Duty Rules
-- ---------------------------------------------------------------------

INSERT INTO duty_rule (duty_id, service_def_id, tier_order, criteria_type,
                       criteria_value, is_active, is_deleted)
SELECT d.duty_id, NULL, 1, 'Role', 'Coordinator', TRUE, FALSE
FROM (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM duty_rule dr
    WHERE dr.duty_id = d.duty_id
      AND dr.tier_order = 1
      AND dr.criteria_type = 'Role'
      AND dr.criteria_value = 'Coordinator'
);

INSERT INTO duty_rule (duty_id, service_def_id, tier_order, criteria_type,
                       criteria_value, is_active, is_deleted)
SELECT d.duty_id, NULL, 1, 'Role', 'Steward', TRUE, FALSE
FROM (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM duty_rule dr
    WHERE dr.duty_id = d.duty_id
      AND dr.tier_order = 1
      AND dr.criteria_type = 'Role'
      AND dr.criteria_value = 'Steward'
);

-- ---------------------------------------------------------------------
-- D6 Eligibility
-- ---------------------------------------------------------------------

INSERT INTO eligibility (member_id, duty_id, granted_date, granted_by,
                         revoked_date, revoked_reason)
SELECT m.member_id, d.duty_id, '2025-01-01', a.admin_id, NULL, NULL
FROM (SELECT member_id FROM member WHERE name = 'Dana' AND surname = 'Prototype' ORDER BY member_id LIMIT 1) m,
     (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d,
     (SELECT a.admin_id
        FROM admin a
        JOIN member am ON am.member_id = a.member_id
       WHERE am.name = 'Alex' AND am.surname = 'Example'
       ORDER BY a.admin_id LIMIT 1) a
WHERE NOT EXISTS (
    SELECT 1 FROM eligibility e
    WHERE e.member_id = m.member_id
      AND e.duty_id = d.duty_id
      AND e.revoked_date IS NULL
);

INSERT INTO eligibility (member_id, duty_id, granted_date, granted_by,
                         revoked_date, revoked_reason)
SELECT m.member_id, d.duty_id, '2025-01-01', a.admin_id, NULL, NULL
FROM (SELECT member_id FROM member WHERE name = 'Beatrice' AND surname = 'Sample' ORDER BY member_id LIMIT 1) m,
     (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d,
     (SELECT a.admin_id
        FROM admin a
        JOIN member am ON am.member_id = a.member_id
       WHERE am.name = 'Alex' AND am.surname = 'Example'
       ORDER BY a.admin_id LIMIT 1) a
WHERE NOT EXISTS (
    SELECT 1 FROM eligibility e
    WHERE e.member_id = m.member_id
      AND e.duty_id = d.duty_id
      AND e.revoked_date IS NULL
);

-- ---------------------------------------------------------------------
-- 8.0 Event, Event Branch, Program, Program Item, Event Duty
--
-- The 8.0 process needs at least one Event with a Program and Program
-- Items so that the process's endpoints and reads have something to
-- operate on. The Event is dated beyond the materialization horizon
-- (seed-vocabulary sets OccurrenceHorizonDays = 30, and the event is
-- dated 2026-12-01) so that 11.0 does not touch it. Seed data is
-- configuration, not operational records; no service_occurrence row is
-- seeded.
-- ---------------------------------------------------------------------

-- Event
INSERT INTO event (name, start_date, end_date, type, host_branch_id,
                   uses_attendance_register, is_active, is_deleted)
SELECT 'Example Conference', '2026-12-01', '2026-12-02', 'Conference',
       b.branch_id, NULL, TRUE, FALSE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b
WHERE NOT EXISTS (
    SELECT 1 FROM event e WHERE e.name = 'Example Conference'
);

-- Event Branch (host included in attending set)
INSERT INTO event_branch (event_id, branch_id)
SELECT e.event_id, b.branch_id
FROM (SELECT event_id FROM event WHERE name = 'Example Conference' ORDER BY event_id LIMIT 1) e,
     (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b
WHERE NOT EXISTS (
    SELECT 1 FROM event_branch eb
    WHERE eb.event_id = e.event_id
      AND eb.branch_id = b.branch_id
);

-- Program (one per Event)
INSERT INTO program (event_id, title, is_deleted)
SELECT e.event_id, 'Main Program', FALSE
FROM (SELECT event_id FROM event WHERE name = 'Example Conference' ORDER BY event_id LIMIT 1) e
WHERE NOT EXISTS (
    SELECT 1 FROM program p WHERE p.event_id = e.event_id
);

-- Program Item — one linked to a Service Definition, one not.
-- The linked item is configuration; the event-sourced ServiceOccurrence
-- that 8.0 would create is not seeded here.

INSERT INTO program_item (program_id, sequence_order, title,
                          scheduled_start, scheduled_end, service_def_id,
                          location, is_deleted)
SELECT p.program_id, 1, 'Opening Service',
       '2026-12-01T09:00:00+02:00'::timestamptz,
       '2026-12-01T10:30:00+02:00'::timestamptz,
       sd.service_def_id, NULL, FALSE
FROM (SELECT pr.program_id
        FROM program pr
        JOIN event e ON e.event_id = pr.event_id
       WHERE e.name = 'Example Conference'
       ORDER BY pr.program_id LIMIT 1) p,
     (SELECT service_def_id FROM service_definition
       WHERE name = 'Sunday Service' ORDER BY service_def_id LIMIT 1) sd
WHERE NOT EXISTS (
    SELECT 1 FROM program_item pi
    WHERE pi.program_id = p.program_id
      AND pi.sequence_order = 1
);

INSERT INTO program_item (program_id, sequence_order, title,
                          scheduled_start, scheduled_end, service_def_id,
                          location, is_deleted)
SELECT p.program_id, 2, 'Closing Remarks',
       '2026-12-02T15:00:00+02:00'::timestamptz,
       '2026-12-02T15:30:00+02:00'::timestamptz,
       NULL, NULL, FALSE
FROM (SELECT pr.program_id
        FROM program pr
        JOIN event e ON e.event_id = pr.event_id
       WHERE e.name = 'Example Conference'
       ORDER BY pr.program_id LIMIT 1) p
WHERE NOT EXISTS (
    SELECT 1 FROM program_item pi
    WHERE pi.program_id = p.program_id
      AND pi.sequence_order = 2
);

-- Event Duty (§4 shape).
--   - Duty on the Opening Service item, scoped to the Sunday Service
--     definition (ServiceDefId set, per-service rules apply).
--   - Duty on the event generally, not scoped (ServiceDefId null,
--     global duty rules apply).
-- Both hang off the Event, not the ProgramItem.

INSERT INTO event_duty (event_id, duty_id, service_def_id,
                        required_slot_count, is_active, is_deleted)
SELECT e.event_id, d.duty_id, sd.service_def_id, 1, TRUE, FALSE
FROM (SELECT event_id FROM event WHERE name = 'Example Conference' ORDER BY event_id LIMIT 1) e,
     (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d,
     (SELECT service_def_id FROM service_definition
       WHERE name = 'Sunday Service' ORDER BY service_def_id LIMIT 1) sd
WHERE NOT EXISTS (
    SELECT 1 FROM event_duty ed
    WHERE ed.event_id = e.event_id
      AND ed.duty_id = d.duty_id
      AND ed.service_def_id = sd.service_def_id
);

INSERT INTO event_duty (event_id, duty_id, service_def_id,
                        required_slot_count, is_active, is_deleted)
SELECT e.event_id, d.duty_id, NULL, 2, TRUE, FALSE
FROM (SELECT event_id FROM event WHERE name = 'Example Conference' ORDER BY event_id LIMIT 1) e,
     (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM event_duty ed
    WHERE ed.event_id = e.event_id
      AND ed.duty_id = d.duty_id
      AND ed.service_def_id IS NULL
);
