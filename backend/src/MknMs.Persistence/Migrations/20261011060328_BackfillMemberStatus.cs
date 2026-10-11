using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMemberStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 6 of docs/database/migration-ordering.md.
            //
            // Creates the default MemberStatus row and assigns it to
            // every member whose MemberStatusId is null. The default
            // row is 'Active', IsRosterable = true, IsDefault = true,
            // per §9.7 and the OI-2 resolution.
            //
            // Event.HostBranchId and EventDuty.DutyId are left null.
            // Their backfill is an administrative action; Step 7 will
            // refuse to add the non-null constraint until the admin
            // has populated them in whatever environment has rows.

            migrationBuilder.Sql(@"
                INSERT INTO member_status (name, is_rosterable, description, is_default, is_active, is_deleted)
                SELECT 'Active', TRUE, 'Default member status', TRUE, TRUE, FALSE
                WHERE NOT EXISTS (
                    SELECT 1 FROM member_status WHERE is_default = TRUE
                );
            ");

            migrationBuilder.Sql(@"
                UPDATE member
                SET member_status_id = (
                    SELECT member_status_id
                    FROM member_status
                    WHERE is_default = TRUE
                    ORDER BY member_status_id
                    LIMIT 1
                )
                WHERE member_status_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
