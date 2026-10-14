
-- MKN-MS development seed data: member register.
--
-- Idempotent. Every insert is guarded by a NOT EXISTS check on the
-- entity's natural key. Lookup joins are pinned to the lowest-id
-- matching row.
--
-- Depends on seed-vocabulary.sql: the Branch, Role and MemberStatus
-- rows must exist before this script runs.
--
-- Source: docs/database/seed-migration.md; spec §4, §9.1.7.

-- ---------------------------------------------------------------------
-- Members
-- Pinned to the lowest-id Main branch, the lowest-id matching Role,
-- and the default MemberStatus.
--
-- Member carries no IsActive column. Its state is MemberStatusId
-- alone. §4, §9.7, §15.8.27. The column was dropped by migration
-- 20261013192223_DropStep8LegacyColumns.
-- ---------------------------------------------------------------------

INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage,
                    gender, phone, email, branch_id, role_id, member_status_id)
SELECT 'Alex', 'Example', '2024-01-15', '1990-05-20', 'Full',
       'Other', '+27-000-0000', 'alex@example.com',
       b.branch_id, r.role_id, s.member_status_id
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Coordinator' ORDER BY role_id LIMIT 1) r,
     (SELECT member_status_id FROM member_status WHERE is_default = TRUE ORDER BY member_status_id LIMIT 1) s
WHERE NOT EXISTS (
    SELECT 1 FROM member m WHERE m.name = 'Alex' AND m.surname = 'Example'
);

INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage,
                    gender, phone, email, branch_id, role_id, member_status_id)
SELECT 'Beatrice', 'Sample', '2024-06-01', '1995-03-12', 'Full',
       'Female', '+27-000-0001', 'beatrice@example.com',
       b.branch_id, r.role_id, s.member_status_id
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Steward' ORDER BY role_id LIMIT 1) r,
     (SELECT member_status_id FROM member_status WHERE is_default = TRUE ORDER BY member_status_id LIMIT 1) s
WHERE NOT EXISTS (
    SELECT 1 FROM member m WHERE m.name = 'Beatrice' AND m.surname = 'Sample'
);

INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage,
                    gender, phone, email, branch_id, role_id, member_status_id)
SELECT 'Casper', 'Instance', '2025-01-10', '2000-08-25', 'Full',
       'Male', '+27-000-0002', 'casper@example.com',
       b.branch_id, r.role_id, s.member_status_id
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Coordinator' ORDER BY role_id LIMIT 1) r,
     (SELECT member_status_id FROM member_status WHERE is_default = TRUE ORDER BY member_status_id LIMIT 1) s
WHERE NOT EXISTS (
    SELECT 1 FROM member m WHERE m.name = 'Casper' AND m.surname = 'Instance'
);

INSERT INTO member (name, surname, join_date, date_of_birth, membership_stage,
                    gender, phone, email, branch_id, role_id, member_status_id)
SELECT 'Dana', 'Prototype', '2023-04-20', '1985-11-30', 'Full',
       'Female', '+27-000-0003', 'dana@example.com',
       b.branch_id, r.role_id, s.member_status_id
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT role_id FROM role WHERE name = 'Steward' ORDER BY role_id LIMIT 1) r,
     (SELECT member_status_id FROM member_status WHERE is_default = TRUE ORDER BY member_status_id LIMIT 1) s
WHERE NOT EXISTS (
    SELECT 1 FROM member m WHERE m.name = 'Dana' AND m.surname = 'Prototype'
);

-- ---------------------------------------------------------------------
-- Admin row for Alex
-- ---------------------------------------------------------------------

INSERT INTO admin (member_id, permission_tier_id, is_active)
SELECT m.member_id, pt.permission_tier_id, TRUE
FROM (SELECT member_id FROM member WHERE name = 'Alex' AND surname = 'Example' ORDER BY member_id LIMIT 1) m,
     (SELECT permission_tier_id FROM permission_tier WHERE name = 'Full Admin' ORDER BY permission_tier_id LIMIT 1) pt
WHERE NOT EXISTS (
    SELECT 1 FROM admin a WHERE a.member_id = m.member_id
);
