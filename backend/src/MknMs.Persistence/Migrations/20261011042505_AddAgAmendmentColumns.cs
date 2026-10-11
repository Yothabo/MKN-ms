using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgAmendmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_program_item_service_definition_service_definition_temp_id",
                table: "program_item");

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "program_item",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "card_number",
                table: "member",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "join_reason",
                table: "member",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "member_status_id",
                table: "member",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "member",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duty_id",
                table: "event_duty",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "service_def_id",
                table: "event_duty",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "host_branch_id",
                table: "event",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "uses_attendance_register",
                table: "event",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "service_def_id",
                table: "duty_rule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "uses_attendance_register",
                table: "branch",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "attendance_record",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "admin",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_member_status",
                table: "member",
                column: "member_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_duty",
                table: "event_duty",
                column: "duty_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_duty_service_def_id",
                table: "event_duty",
                column: "service_def_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_host_branch",
                table: "event",
                column: "host_branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_duty_rule_service_def_id",
                table: "duty_rule",
                column: "service_def_id");

            migrationBuilder.AddForeignKey(
                name: "fk_duty_rule_service_definitions_service_definition_temp_id",
                table: "duty_rule",
                column: "service_def_id",
                principalTable: "service_definition",
                principalColumn: "service_def_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_event_branch_host_branch_id",
                table: "event",
                column: "host_branch_id",
                principalTable: "branch",
                principalColumn: "branch_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_duty_duty_id",
                table: "event_duty",
                column: "duty_id",
                principalTable: "duty",
                principalColumn: "duty_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_service_definition_service_definition_temp_id1",
                table: "event_duty",
                column: "service_def_id",
                principalTable: "service_definition",
                principalColumn: "service_def_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_member_member_statuses_member_status_id",
                table: "member",
                column: "member_status_id",
                principalTable: "member_status",
                principalColumn: "member_status_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_program_item_service_definition_service_definition_temp_id2",
                table: "program_item",
                column: "service_def_id",
                principalTable: "service_definition",
                principalColumn: "service_def_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_duty_rule_service_definitions_service_definition_temp_id",
                table: "duty_rule");

            migrationBuilder.DropForeignKey(
                name: "fk_event_branch_host_branch_id",
                table: "event");

            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_duty_duty_id",
                table: "event_duty");

            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_service_definition_service_definition_temp_id1",
                table: "event_duty");

            migrationBuilder.DropForeignKey(
                name: "fk_member_member_statuses_member_status_id",
                table: "member");

            migrationBuilder.DropForeignKey(
                name: "fk_program_item_service_definition_service_definition_temp_id2",
                table: "program_item");

            migrationBuilder.DropIndex(
                name: "ix_member_status",
                table: "member");

            migrationBuilder.DropIndex(
                name: "ix_event_duty_duty",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "IX_event_duty_service_def_id",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "ix_event_host_branch",
                table: "event");

            migrationBuilder.DropIndex(
                name: "IX_duty_rule_service_def_id",
                table: "duty_rule");

            migrationBuilder.DropColumn(
                name: "location",
                table: "program_item");

            migrationBuilder.DropColumn(
                name: "card_number",
                table: "member");

            migrationBuilder.DropColumn(
                name: "join_reason",
                table: "member");

            migrationBuilder.DropColumn(
                name: "member_status_id",
                table: "member");

            migrationBuilder.DropColumn(
                name: "receipt_number",
                table: "member");

            migrationBuilder.DropColumn(
                name: "duty_id",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "service_def_id",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "host_branch_id",
                table: "event");

            migrationBuilder.DropColumn(
                name: "uses_attendance_register",
                table: "event");

            migrationBuilder.DropColumn(
                name: "service_def_id",
                table: "duty_rule");

            migrationBuilder.DropColumn(
                name: "uses_attendance_register",
                table: "branch");

            migrationBuilder.DropColumn(
                name: "source",
                table: "attendance_record");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "admin");

            migrationBuilder.AddForeignKey(
                name: "fk_program_item_service_definition_service_definition_temp_id",
                table: "program_item",
                column: "service_def_id",
                principalTable: "service_definition",
                principalColumn: "service_def_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
