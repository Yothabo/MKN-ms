using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRegisterPhaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "can_attend_register",
                table: "member_status",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "attendance_correction",
                columns: table => new
                {
                    correction_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    attendance_record_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    corrected_by = table.Column<int>(type: "integer", nullable: false),
                    corrected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_correction", x => x.correction_id);
                    table.ForeignKey(
                        name: "fk_attendance_correction_admins_corrected_by_admin_admin_id",
                        column: x => x.corrected_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_correction_attendance_records_attendance_record_id",
                        column: x => x.attendance_record_id,
                        principalTable: "attendance_record",
                        principalColumn: "record_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_correction_corrected_by",
                table: "attendance_correction",
                column: "corrected_by");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_correction_record",
                table: "attendance_correction",
                column: "attendance_record_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_correction");

            migrationBuilder.DropColumn(
                name: "can_attend_register",
                table: "member_status");
        }
    }
}
