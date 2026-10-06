-- MKN-MS development seed data for exercising process 5.0.
--
-- Idempotent: safe to re-run. Every insert is guarded by a NOT EXISTS
-- check on the entity's natural key. ON CONFLICT DO NOTHING is not used
-- in this file, because names are not unique in this schema and the
-- guard would not fire.
--
-- All lookup joins are pinned to the lowest-id matching row, so a
-- pre-existing duplicate in a lookup table does not multiply the rows
-- this script inserts.

-- ---------------------------------------------------------------------
-- Additional members, so Duty Rule filtering has something to filter.
-- seed-development.sql already inserts Alex Example (Coordinator).
-- ---------------------------------------------------------------------

-- Beatrice — Steward.
INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage, gender, phone, email, branch_id, role_id, is_active)
SELECT 'Beatrice', 'Sample', '2024-06-01', '1995-03-12', 'Full', 'Female', '+27-000-0001', 'beatrice@example.com', b.branch_id, r.role_id, TRUE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Steward' ORDER BY role_id LIMIT 1) r
WHERE NOT EXISTS (
    SELECT 1 FROM member m
    WHERE m.name = 'Beatrice' AND m.surname = 'Sample'
);

-- Casper — Coordinator.
INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage, gender, phone, email, branch_id, role_id, is_active)
SELECT 'Casper', 'Instance', '2025-01-10', '2000-08-25', 'Full', 'Male', '+27-000-0002', 'casper@example.com', b.branch_id, r.role_id, TRUE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Coordinator' ORDER BY role_id LIMIT 1) r
WHERE NOT EXISTS (
    SELECT 1 FROM member m
    WHERE m.name = 'Casper' AND m.surname = 'Instance'
);

-- Dana — Steward.
INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage, gender, phone, email, branch_id, role_id, is_active)
SELECT 'Dana', 'Prototype', '2023-04-20', '1985-11-30', 'Full', 'Female', '+27-000-0003', 'dana@example.com', b.branch_id, r.role_id, TRUE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Steward' ORDER BY role_id LIMIT 1) r
WHERE NOT EXISTS (
    SELECT 1 FROM member m
    WHERE m.name = 'Dana' AND m.surname = 'Prototype'
);

-- ---------------------------------------------------------------------
-- Admin row for Alex, so eligibility grants have an authorizing admin.
-- ---------------------------------------------------------------------
INSERT INTO admin (member_id, permission_tier_id)
SELECT m.member_id, pt.permission_tier_id
FROM (SELECT member_id FROM member WHERE name = 'Alex' AND surname = 'Example' ORDER BY member_id LIMIT 1) m,
     (SELECT permission_tier_id FROM permission_tier WHERE name = 'Full Admin' ORDER BY permission_tier_id LIMIT 1) pt
WHERE NOT EXISTS (
    SELECT 1 FROM admin a WHERE a.member_id = m.member_id
);

-- ---------------------------------------------------------------------
-- Service Definition Duty — attach Welcome and Reading duties to the
-- Regular Sunday Service.
-- ---------------------------------------------------------------------

-- Welcome duty on Sunday Service.
INSERT INTO service_definition_duty (service_def_id, duty_id, required_slot_count, is_active)
SELECT sd.service_def_id, d.duty_id, 1, TRUE
FROM (SELECT sd.service_def_id
        FROM service_definition sd
        JOIN service_type st ON st.service_type_id = sd.service_type_id
       WHERE sd.name = 'Sunday Service' AND st.name = 'Regular'
       ORDER BY sd.service_def_id LIMIT 1) sd,
     (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition_duty sdd
    WHERE sdd.service_def_id = sd.service_def_id
      AND sdd.duty_id = d.duty_id
);

-- Reading duty on Sunday Service.
INSERT INTO service_definition_duty (service_def_id, duty_id, required_slot_count, is_active)
SELECT sd.service_def_id, d.duty_id, 1, TRUE
FROM (SELECT sd.service_def_id
        FROM service_definition sd
        JOIN service_type st ON st.service_type_id = sd.service_type_id
       WHERE sd.name = 'Sunday Service' AND st.name = 'Regular'
       ORDER BY sd.service_def_id LIMIT 1) sd,
     (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition_duty sdd
    WHERE sdd.service_def_id = sd.service_def_id
      AND sdd.duty_id = d.duty_id
);

-- ---------------------------------------------------------------------
-- Eligibility grants.
-- Welcome duty requires an explicit Eligibility grant from an admin.
-- Dana is granted eligibility; Alex and Casper are not.
-- ---------------------------------------------------------------------

-- Dana → Welcome duty.
INSERT INTO eligibility (member_id, duty_id, granted_date, granted_by, revoked_date, revoked_reason)
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

-- Beatrice → Reading duty.
INSERT INTO eligibility (member_id, duty_id, granted_date, granted_by, revoked_date, revoked_reason)
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
-- Duty Rule for Reading duty — Tier 1 prefers Steward role.
-- ---------------------------------------------------------------------
INSERT INTO duty_rule (duty_id, tier_order, criteria_type, criteria_value, is_active)
SELECT d.duty_id, 1, 'Role', 'Steward', TRUE
FROM (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM duty_rule dr
    WHERE dr.duty_id = d.duty_id
      AND dr.tier_order = 1
      AND dr.criteria_type = 'Role'
      AND dr.criteria_value = 'Steward'
);
