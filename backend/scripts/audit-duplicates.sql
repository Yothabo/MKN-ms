-- Audit duplicate names across every vocabulary table the §15.8.40
-- amendment constrains.
--
-- Read-only. Run this before applying the amendment's migration. If it
-- returns rows, resolve each duplicate (delete or soft-delete the
-- unwanted row) before migrating — the migration's unique indexes will
-- otherwise fail with a clear Postgres error, which is the correct
-- refusal, but resolving first is the operator's job.
--
-- Member is deliberately excluded from this audit. The amendment imposes
-- no unique constraint on Member. A separate report identifies candidate
-- member duplicates for administrator reconciliation; that report is
-- advisory, not a migration target.

WITH all_names AS (
    SELECT 'role'             AS table_name, name FROM role
    UNION ALL SELECT 'duty',              name FROM duty
    UNION ALL SELECT 'branch',            name FROM branch
    UNION ALL SELECT 'permission_tier',   name FROM permission_tier
    UNION ALL SELECT 'member_status',     name FROM member_status
    UNION ALL SELECT 'time_of_day',       name FROM time_of_day
    UNION ALL SELECT 'service_type',      name FROM service_type
    UNION ALL SELECT 'outcome_state',     name FROM outcome_state
    UNION ALL SELECT 'assignment_status', name FROM assignment_status
    UNION ALL SELECT 'capability',        name FROM capability
    UNION ALL SELECT 'attribute_type',    name FROM attribute_type
)
SELECT
    table_name,
    lower(name) AS normalized_name,
    COUNT(*)    AS duplicate_count,
    array_agg(name) AS raw_names
FROM all_names
GROUP BY table_name, lower(name)
HAVING COUNT(*) > 1
ORDER BY table_name, normalized_name;

-- ServiceDefinition: two partial indexes will be created. Audit each
-- scope separately so the operator can tell global duplicates apart
-- from branch-scoped duplicates.

SELECT
    'service_definition (global)' AS scope,
    lower(name) AS normalized_name,
    COUNT(*)    AS duplicate_count,
    array_agg(service_def_id ORDER BY service_def_id) AS service_ids
FROM service_definition
WHERE owning_branch_id IS NULL
  AND is_deleted = FALSE
GROUP BY lower(name)
HAVING COUNT(*) > 1
ORDER BY normalized_name;

SELECT
    'service_definition (per branch)' AS scope,
    owning_branch_id,
    lower(name) AS normalized_name,
    COUNT(*)    AS duplicate_count,
    array_agg(service_def_id ORDER BY service_def_id) AS service_ids
FROM service_definition
WHERE owning_branch_id IS NOT NULL
  AND is_deleted = FALSE
GROUP BY owning_branch_id, lower(name)
HAVING COUNT(*) > 1
ORDER BY owning_branch_id, normalized_name;

-- Advisory: candidate member duplicates for reconciliation. No
-- constraint will be added on member. This report exists so an
-- administrator can decide whether two rows are two people (twins,
-- same-name cousins) or one person entered twice, and then set the
-- unwanted row's member_status_id to a non-rosterable status.

SELECT
    lower(name)    AS name_lc,
    lower(surname) AS surname_lc,
    date_of_birth,
    COUNT(*)       AS n,
    array_agg(member_id ORDER BY member_id) AS member_ids
FROM member
GROUP BY lower(name), lower(surname), date_of_birth
HAVING COUNT(*) > 1
ORDER BY n DESC, name_lc, surname_lc;
