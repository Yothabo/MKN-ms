
-- MKN-MS development seed data: base vocabulary.
--
-- Idempotent. Every insert is guarded by a NOT EXISTS check on the
-- entity's natural key, except system_setting where the key is the
-- primary key and ON CONFLICT DO NOTHING is the correct guard.
--
-- Lookup joins are pinned to the lowest-id matching row, so a
-- pre-existing duplicate in a lookup table does not multiply the
-- rows this script inserts.
--
-- Run order: seed-vocabulary.sql, then seed-membership.sql, then
-- seed-operations.sql.
--
-- Source: docs/database/seed-migration.md; spec §4, §15.

-- ---------------------------------------------------------------------
-- D10 lookups
-- ---------------------------------------------------------------------

INSERT INTO time_of_day (name, is_active, is_deleted)
SELECT 'Morning', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM time_of_day WHERE name = 'Morning');

INSERT INTO time_of_day (name, is_active, is_deleted)
SELECT 'Evening', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM time_of_day WHERE name = 'Evening');

INSERT INTO service_type (name, is_active, is_deleted)
SELECT 'Regular', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM service_type WHERE name = 'Regular');

INSERT INTO service_type (name, is_active, is_deleted)
SELECT 'Special', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM service_type WHERE name = 'Special');

INSERT INTO outcome_state (name, is_active, is_deleted)
SELECT 'Unfilled', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Unfilled');

INSERT INTO outcome_state (name, is_active, is_deleted)
SELECT 'Partially Filled', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Partially Filled');

INSERT INTO outcome_state (name, is_active, is_deleted)
SELECT 'Filled', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Filled');

INSERT INTO outcome_state (name, is_active, is_deleted)
SELECT 'Cancelled', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM outcome_state WHERE name = 'Cancelled');

INSERT INTO assignment_status (name, is_terminal, is_active, is_deleted)
SELECT 'Proposed', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Proposed');

INSERT INTO assignment_status (name, is_terminal, is_active, is_deleted)
SELECT 'Confirmed', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Confirmed');

INSERT INTO assignment_status (name, is_terminal, is_active, is_deleted)
SELECT 'Declined', TRUE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Declined');

INSERT INTO assignment_status (name, is_terminal, is_active, is_deleted)
SELECT 'Timed Out', TRUE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM assignment_status WHERE name = 'Timed Out');

INSERT INTO permission_tier (name, is_default, is_active, is_deleted)
SELECT 'Full Admin', TRUE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM permission_tier WHERE name = 'Full Admin');

INSERT INTO permission_tier (name, is_default, is_active, is_deleted)
SELECT 'Roster Admin', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM permission_tier WHERE name = 'Roster Admin');

-- ---------------------------------------------------------------------
-- D4 Member Status
-- ---------------------------------------------------------------------

INSERT INTO member_status (name, is_rosterable, can_attend_register, description, is_default, is_active, is_deleted)
SELECT 'Active', TRUE, TRUE, 'Default member status', TRUE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM member_status WHERE name = 'Active');

INSERT INTO member_status (name, is_rosterable, can_attend_register, description, is_default, is_active, is_deleted)
SELECT 'Inactive', FALSE, FALSE, 'Not rosterable', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM member_status WHERE name = 'Inactive');

INSERT INTO member_status (name, is_rosterable, can_attend_register, description, is_default, is_active, is_deleted)
SELECT 'Deceased', FALSE, FALSE, 'Deceased members are not attendable', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM member_status WHERE name = 'Deceased');

-- ---------------------------------------------------------------------
-- D1 Role and Duty
-- ---------------------------------------------------------------------

INSERT INTO role (name, is_default, is_active, is_deleted)
SELECT 'Coordinator', FALSE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE name = 'Coordinator');

INSERT INTO role (name, is_default, is_active, is_deleted)
SELECT 'Steward', TRUE, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM role WHERE name = 'Steward');

INSERT INTO duty (name, is_active, is_deleted)
SELECT 'Welcome duty', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM duty WHERE name = 'Welcome duty');

INSERT INTO duty (name, is_active, is_deleted)
SELECT 'Reading duty', TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM duty WHERE name = 'Reading duty');

-- ---------------------------------------------------------------------
-- D3 Branch
-- ---------------------------------------------------------------------

INSERT INTO branch (name, location, uses_attendance_register, is_active, is_deleted)
SELECT 'Main', 'Main Street', NULL, TRUE, FALSE
WHERE NOT EXISTS (SELECT 1 FROM branch WHERE name = 'Main');

-- ---------------------------------------------------------------------
-- D3 Branch Time Slot
-- Pinned to the lowest-id Main branch and lowest-id Morning lookup.
-- ---------------------------------------------------------------------

INSERT INTO branch_time_slot (branch_id, day_of_week, time_of_day_id, is_active, is_deleted)
SELECT b.branch_id, 'Sunday', t.time_of_day_id, TRUE, FALSE
FROM (SELECT branch_id FROM branch WHERE name = 'Main' ORDER BY branch_id LIMIT 1) b,
     (SELECT time_of_day_id FROM time_of_day WHERE name = 'Morning' ORDER BY time_of_day_id LIMIT 1) t
WHERE NOT EXISTS (
    SELECT 1 FROM branch_time_slot bts
    WHERE bts.branch_id = b.branch_id
      AND bts.day_of_week = 'Sunday'
      AND bts.time_of_day_id = t.time_of_day_id
);

-- ---------------------------------------------------------------------
-- D3 Service Definition and its duties
-- ---------------------------------------------------------------------

INSERT INTO service_definition (name, service_type_id, owning_branch_id, is_active, is_deleted)
SELECT 'Sunday Service', st.service_type_id, NULL, TRUE, FALSE
FROM (SELECT service_type_id FROM service_type WHERE name = 'Regular' ORDER BY service_type_id LIMIT 1) st
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition sd
    WHERE sd.name = 'Sunday Service'
      AND sd.service_type_id = st.service_type_id
);

INSERT INTO service_definition_duty (service_def_id, duty_id, required_slot_count, is_active, is_deleted)
SELECT sd.service_def_id, d.duty_id, 1, TRUE, FALSE
FROM (SELECT service_def_id FROM service_definition WHERE name = 'Sunday Service' ORDER BY service_def_id LIMIT 1) sd,
     (SELECT duty_id FROM duty WHERE name = 'Welcome duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition_duty sdd
    WHERE sdd.service_def_id = sd.service_def_id
      AND sdd.duty_id = d.duty_id
);

INSERT INTO service_definition_duty (service_def_id, duty_id, required_slot_count, is_active, is_deleted)
SELECT sd.service_def_id, d.duty_id, 1, TRUE, FALSE
FROM (SELECT service_def_id FROM service_definition WHERE name = 'Sunday Service' ORDER BY service_def_id LIMIT 1) sd,
     (SELECT duty_id FROM duty WHERE name = 'Reading duty' ORDER BY duty_id LIMIT 1) d
WHERE NOT EXISTS (
    SELECT 1 FROM service_definition_duty sdd
    WHERE sdd.service_def_id = sd.service_def_id
      AND sdd.duty_id = d.duty_id
);

-- ---------------------------------------------------------------------
-- D3 Service Schedule
-- ---------------------------------------------------------------------

INSERT INTO service_schedule (service_def_id, time_slot_id, start_time, is_active, is_deleted)
SELECT sd.service_def_id, bts.time_slot_id, '09:00', TRUE, FALSE
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
-- D11 System Setting
-- The key is the primary key, so ON CONFLICT DO NOTHING is the
-- correct guard here.
-- ---------------------------------------------------------------------

INSERT INTO system_setting (key, value, required, description)
VALUES ('OccurrenceHorizonDays', '30', TRUE,
        'Rolling horizon in days for occurrence materialization')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('InitialAssignmentStatusID', '2', TRUE,
        'The AssignmentStatus a new assignment transitions to on confirmation')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('DeclinedStatusID', '3', TRUE,
        'The AssignmentStatus a declined assignment transitions to')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('TimedOutStatusID', '4', TRUE,
        'The AssignmentStatus a timed-out assignment transitions to')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('ConfirmationTimeoutHours', '48', TRUE,
        'Hours after which an unresponded assignment is considered timed out')
ON CONFLICT (key) DO NOTHING;

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

INSERT INTO system_setting (key, value, required, description)
VALUES ('OutcomeStateCancelledID', '4', FALSE,
        'OutcomeState marking an occurrence as cancelled; skipped by 10.0')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('NotificationChannel', 'Log', FALSE,
        'The channel 9.0 dispatches assignment notices through')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('ApplicationTimeZone', 'Africa/Johannesburg', TRUE,
        'IANA timezone ID used to derive operational calendar dates')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('AttendanceRegisterEnabled', 'true', TRUE,
        'Global default for the attendance register')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('ReceiptToCardDurationDays', '90', FALSE,
        'Days a receipt is held before a card is issued')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('YouthAgeMin', '13', TRUE,
        'Minimum age at which a member counts as a youth')
ON CONFLICT (key) DO NOTHING;

INSERT INTO system_setting (key, value, required, description)
VALUES ('YouthAgeMax', '35', TRUE,
        'Maximum age at which a member counts as a youth')
ON CONFLICT (key) DO NOTHING;

-- ---------------------------------------------------------------------
-- D14 Entity Deletion Policy — NotificationSubscription
--
-- §9.7 requires a policy row for every configuration entity that
-- offers a soft delete. NotificationSubscription carries both
-- IsActive and IsDeleted; the fail-closed rule from the 1.8 work
-- item refuses a lifecycle operation on an entity type with no
-- policy row. Reason is required for soft delete; approval is not.
-- ---------------------------------------------------------------------

INSERT INTO entity_deletion_policy (entity_type, requires_approval_for_delete,
                                    requires_approval_for_deactivate,
                                    required_delete_permission_tier_id,
                                    required_deactivate_permission_tier_id,
                                    requires_reason_for_delete,
                                    requires_reason_for_deactivate,
                                    is_active, is_deleted)
SELECT 'NotificationSubscription', FALSE, FALSE, NULL, NULL, TRUE, FALSE, TRUE, FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM entity_deletion_policy WHERE entity_type = 'NotificationSubscription'
);

-- ---------------------------------------------------------------------
-- Default-flag backfill
--
-- §9.7 refers to 'the default Role' and 'the default Member Status'.
-- §4 gives Role and PermissionTier an IsDefault column and the OI-2
-- ruling gives MemberStatus one. When the column was added by migration,
-- existing rows received DEFAULT FALSE, so a table that already carried
-- rows has no row with IsDefault = true. The three updates below set
-- the lowest-id row as the default, guarded so they are idempotent and
-- do not overwrite an already-chosen default.
-- ---------------------------------------------------------------------

UPDATE role
SET is_default = TRUE
WHERE role_id = (SELECT MIN(role_id) FROM role)
  AND NOT EXISTS (SELECT 1 FROM role WHERE is_default = TRUE);

UPDATE member_status
SET is_default = TRUE
WHERE member_status_id = (SELECT MIN(member_status_id) FROM member_status)
  AND NOT EXISTS (SELECT 1 FROM member_status WHERE is_default = TRUE);

UPDATE permission_tier
SET is_default = TRUE
WHERE permission_tier_id = (SELECT MIN(permission_tier_id) FROM permission_tier)
  AND NOT EXISTS (SELECT 1 FROM permission_tier WHERE is_default = TRUE);


-- ---------------------------------------------------------------------
-- D14 Entity Deletion Policy
--
-- One row per configuration entity type that can be deactivated or
-- soft-deleted. §9.7 requires the policy to govern every such
-- operation; a missing policy row is a configuration error. The seed
-- provides a complete set so that the fail-closed behaviour the
-- ConfigurationLifecycleService enforces has a policy to resolve.
--
-- Approval: soft delete requires a second admin's approval;
--           deactivate does not (reversible).
-- Reason:    soft delete requires a reason; deactivate does not.
-- Tier:      no specific tier is required by the seed. Deployments
--            that want a stricter policy set RequiredDeletePermissionTierId
--            on the row for the entity type.
--
-- Idempotent. Guarded by NOT EXISTS on (entity_type).
-- ---------------------------------------------------------------------

INSERT INTO entity_deletion_policy (entity_type, requires_approval_for_delete,
                                    requires_approval_for_deactivate,
                                    required_delete_permission_tier_id,
                                    required_deactivate_permission_tier_id,
                                    requires_reason_for_delete,
                                    requires_reason_for_deactivate,
                                    is_active, is_deleted)
SELECT entity_type, TRUE, FALSE, NULL, NULL, TRUE, FALSE, TRUE, FALSE
FROM (VALUES
    ('Role'),
    ('Duty'),
    ('Branch'),
    ('BranchTimeSlot'),
    ('ServiceDefinition'),
    ('ServiceDefinitionDuty'),
    ('ServiceSchedule'),
    ('DutyRule'),
    ('Event'),
    ('Program'),
    ('ProgramItem'),
    ('EventDuty'),
    ('MemberStatus'),
    ('AttributeType'),
    ('OutcomeState'),
    ('AssignmentStatus'),
    ('TimeOfDay'),
    ('ServiceType'),
    ('PermissionTier'),
    ('AttendanceRule')
) AS v(entity_type)
WHERE NOT EXISTS (
    SELECT 1 FROM entity_deletion_policy p
    WHERE p.entity_type = v.entity_type
);
