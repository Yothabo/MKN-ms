using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "uq_role_single_default",
                table: "role",
                column: "is_default",
                unique: true,
                filter: "is_default = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_permission_tier_single_default",
                table: "permission_tier",
                column: "is_default",
                unique: true,
                filter: "is_default = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_member_status_single_default",
                table: "member_status",
                column: "is_default",
                unique: true,
                filter: "is_default = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_role_single_default",
                table: "role");

            migrationBuilder.DropIndex(
                name: "uq_permission_tier_single_default",
                table: "permission_tier");

            migrationBuilder.DropIndex(
                name: "uq_member_status_single_default",
                table: "member_status");
        }
    }
}
