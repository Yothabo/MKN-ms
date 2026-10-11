using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "time_of_day",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "time_of_day",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "service_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "service_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "service_schedule",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "service_definition_duty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "service_definition",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "role",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "program_item",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "program",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "permission_tier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "permission_tier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "permission_tier",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "outcome_state",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "outcome_state",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "event_duty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "event_duty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "event",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "duty_rule",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "duty",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "branch_time_slot",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "branch",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "assignment_status",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "assignment_status",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "time_of_day");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "time_of_day");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "service_type");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "service_type");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "service_schedule");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "service_definition_duty");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "service_definition");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "role");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "role");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "program_item");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "program");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "permission_tier");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "permission_tier");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "permission_tier");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "outcome_state");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "outcome_state");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "event");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "duty_rule");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "duty");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "branch_time_slot");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "branch");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "assignment_status");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "assignment_status");
        }
    }
}
