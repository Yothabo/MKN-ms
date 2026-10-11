
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
