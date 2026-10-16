using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreEventDutyToSpec4Shape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the foreign keys and indexes of the three legacy
            // columns before the columns themselves.
            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_assignment_status_assignment_status_id",
                table: "event_duty");

            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_member_assigned_member_id",
                table: "event_duty");

            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_program_items_program_item_id",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "ix_event_duty_assignment_status_id",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "ix_event_duty_program_item",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "ix_event_duty_assigned_member",
                table: "event_duty");

            // Add the two new columns as nullable so existing rows can
            // be backfilled before the not-null constraint is applied.
            migrationBuilder.AddColumn<int>(
                name: "event_id",
                table: "event_duty",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "required_slot_count",
                table: "event_duty",
                type: "integer",
                nullable: true);

            // Backfill event_id from the existing ProgramItem -> Program
            // -> Event chain. Deterministic and lossless. No-op on an
            // empty table.
            migrationBuilder.Sql(@"
                UPDATE event_duty ed
                SET event_id = (
                    SELECT p.event_id
                    FROM program_item pi
                    JOIN program p ON p.program_id = pi.program_id
                    WHERE pi.item_id = ed.program_item_id
                )
                WHERE event_id IS NULL;
            ");

            // required_slot_count has no source in the pre-§4 schema.
            // Default to 1 for any pre-existing rows. No-op on an empty
            // table.
            migrationBuilder.Sql(@"
                UPDATE event_duty
                SET required_slot_count = 1
                WHERE required_slot_count IS NULL;
            ");

            // Tighten to not-null now that every row has a value.
            migrationBuilder.AlterColumn<int>(
                name: "event_id",
                table: "event_duty",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "required_slot_count",
                table: "event_duty",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // Drop the three legacy columns.
            migrationBuilder.DropColumn(
                name: "program_item_id",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "assigned_member_id",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "assignment_status_id",
                table: "event_duty");

            // Index and foreign key for the new Event relationship.
            migrationBuilder.CreateIndex(
                name: "ix_event_duty_event",
                table: "event_duty",
                column: "event_id");

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_event_event_id",
                table: "event_duty",
                column: "event_id",
                principalTable: "event",
                principalColumn: "event_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the Up. Data in the three legacy columns cannot be
            // recovered — it was dropped. The columns are restored with
            // null / default values as the schema permits.
            migrationBuilder.DropForeignKey(
                name: "fk_event_duty_event_event_id",
                table: "event_duty");

            migrationBuilder.DropIndex(
                name: "ix_event_duty_event",
                table: "event_duty");

            migrationBuilder.AddColumn<int>(
                name: "program_item_id",
                table: "event_duty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "assigned_member_id",
                table: "event_duty",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "assignment_status_id",
                table: "event_duty",
                type: "integer",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "required_slot_count",
                table: "event_duty");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_program_item",
                table: "event_duty",
                column: "program_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_assigned_member",
                table: "event_duty",
                column: "assigned_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_assignment_status_id",
                table: "event_duty",
                column: "assignment_status_id");

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_program_items_program_item_id",
                table: "event_duty",
                column: "program_item_id",
                principalTable: "program_item",
                principalColumn: "item_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_member_assigned_member_id",
                table: "event_duty",
                column: "assigned_member_id",
                principalTable: "member",
                principalColumn: "member_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_event_duty_assignment_status_assignment_status_id",
                table: "event_duty",
                column: "assignment_status_id",
                principalTable: "assignment_status",
                principalColumn: "assignment_status_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
