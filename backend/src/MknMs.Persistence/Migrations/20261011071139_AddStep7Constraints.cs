using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStep7Constraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "member_status_id",
                table: "member",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "duty_id",
                table: "event_duty",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "host_branch_id",
                table: "event",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // ---- Lifecycle-flag mutual exclusion ----
            //
            // §9.7: IsActive = true and IsDeleted = true is a forbidden
            // state. Applied to every entity that carries both flags.

            AddFlagCheck(migrationBuilder, "role");
            AddFlagCheck(migrationBuilder, "duty");
            AddFlagCheck(migrationBuilder, "duty_rule");
            AddFlagCheck(migrationBuilder, "branch");
            AddFlagCheck(migrationBuilder, "branch_time_slot");
            AddFlagCheck(migrationBuilder, "service_definition");
            AddFlagCheck(migrationBuilder, "service_definition_duty");
            AddFlagCheck(migrationBuilder, "service_schedule");
            AddFlagCheck(migrationBuilder, "event");
            AddFlagCheck(migrationBuilder, "event_duty");
            AddFlagCheck(migrationBuilder, "member_status");
            AddFlagCheck(migrationBuilder, "attribute_type");
            AddFlagCheck(migrationBuilder, "capability");
            AddFlagCheck(migrationBuilder, "attendance_rule");
            AddFlagCheck(migrationBuilder, "notification_subscription");
            AddFlagCheck(migrationBuilder, "entity_deletion_policy");
            AddFlagCheck(migrationBuilder, "outcome_state");
            AddFlagCheck(migrationBuilder, "assignment_status");
            AddFlagCheck(migrationBuilder, "permission_tier");
            AddFlagCheck(migrationBuilder, "service_type");
            AddFlagCheck(migrationBuilder, "time_of_day");

            // ---- ServiceOccurrence provenance ----
            //
            // §4 (Service Occurrence): exactly one of schedule_id or
            // event_id is non-null. B18.

            migrationBuilder.Sql(@"
                ALTER TABLE service_occurrence
                ADD CONSTRAINT chk_service_occurrence_source
                CHECK (
                    (schedule_id IS NOT NULL AND event_id IS NULL)
                    OR (schedule_id IS NULL AND event_id IS NOT NULL)
                );
            ");
        }

        private static void AddFlagCheck(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql(
                "ALTER TABLE " + table + " "
                + "ADD CONSTRAINT chk_" + table + "_flags "
                + "CHECK (NOT (is_active = TRUE AND is_deleted = TRUE));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "member_status_id",
                table: "member",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "duty_id",
                table: "event_duty",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "host_branch_id",
                table: "event",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(
                "ALTER TABLE service_occurrence "
                + "DROP CONSTRAINT IF EXISTS chk_service_occurrence_source;");

            DropFlagCheck(migrationBuilder, "time_of_day");
            DropFlagCheck(migrationBuilder, "service_type");
            DropFlagCheck(migrationBuilder, "permission_tier");
            DropFlagCheck(migrationBuilder, "assignment_status");
            DropFlagCheck(migrationBuilder, "outcome_state");
            DropFlagCheck(migrationBuilder, "entity_deletion_policy");
            DropFlagCheck(migrationBuilder, "notification_subscription");
            DropFlagCheck(migrationBuilder, "attendance_rule");
            DropFlagCheck(migrationBuilder, "capability");
            DropFlagCheck(migrationBuilder, "attribute_type");
            DropFlagCheck(migrationBuilder, "member_status");
            DropFlagCheck(migrationBuilder, "event_duty");
            DropFlagCheck(migrationBuilder, "event");
            DropFlagCheck(migrationBuilder, "service_schedule");
            DropFlagCheck(migrationBuilder, "service_definition_duty");
            DropFlagCheck(migrationBuilder, "service_definition");
            DropFlagCheck(migrationBuilder, "branch_time_slot");
            DropFlagCheck(migrationBuilder, "branch");
            DropFlagCheck(migrationBuilder, "duty_rule");
            DropFlagCheck(migrationBuilder, "duty");
            DropFlagCheck(migrationBuilder, "role");
        }

        private static void DropFlagCheck(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql(
                "ALTER TABLE " + table + " "
                + "DROP CONSTRAINT IF EXISTS chk_" + table + "_flags;");
        }
    }
}
