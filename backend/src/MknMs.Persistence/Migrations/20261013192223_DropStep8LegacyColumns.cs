using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropStep8LegacyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "member");

            migrationBuilder.DropColumn(
                name: "label",
                table: "event_duty");

            migrationBuilder.DropColumn(
                name: "location",
                table: "event");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "member",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "event_duty",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "event",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
