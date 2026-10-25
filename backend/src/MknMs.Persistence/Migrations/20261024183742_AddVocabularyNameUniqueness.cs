using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularyNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // §15.8.40: case-insensitive uniqueness on every vocabulary
            // entity's name among live rows (is_deleted = false).
            //
            // Expressed as raw SQL because EF Core's index builder does
            // not natively emit an expression index over lower(name).
            // Each index is partial: soft-deleted rows are excluded so a
            // retired name can be reused for a new live row.

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_role_live_name_lower "
                + "ON role (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_duty_live_name_lower "
                + "ON duty (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_branch_live_name_lower "
                + "ON branch (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_permission_tier_live_name_lower "
                + "ON permission_tier (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_member_status_live_name_lower "
                + "ON member_status (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_time_of_day_live_name_lower "
                + "ON time_of_day (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_service_type_live_name_lower "
                + "ON service_type (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_outcome_state_live_name_lower "
                + "ON outcome_state (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_assignment_status_live_name_lower "
                + "ON assignment_status (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_capability_live_name_lower "
                + "ON capability (lower(name)) WHERE is_deleted = FALSE;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_attribute_type_live_name_lower "
                + "ON attribute_type (lower(name)) WHERE is_deleted = FALSE;");

            // ServiceDefinition: two filtered indexes for the two scopes.
            // OwningBranchId null means global; set means branch-scoped.
            // A global and a branch-scoped service may both be called
            // "Sunday Service" — they are different services at different
            // scopes. Within each scope, the name is unique among live rows.

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_service_definition_global_live_name_lower "
                + "ON service_definition (lower(name)) "
                + "WHERE is_deleted = FALSE AND owning_branch_id IS NULL;");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX uq_service_definition_scoped_live_name_lower "
                + "ON service_definition (owning_branch_id, lower(name)) "
                + "WHERE is_deleted = FALSE AND owning_branch_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_service_definition_scoped_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_service_definition_global_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_attribute_type_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_capability_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_assignment_status_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_outcome_state_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_service_type_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_time_of_day_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_member_status_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_permission_tier_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_branch_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_duty_live_name_lower;");
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS uq_role_live_name_lower;");
        }
    }
}
