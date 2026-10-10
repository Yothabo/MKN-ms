using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRuleAndAgAmendmentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attribute_type",
                columns: table => new
                {
                    attribute_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attribute_type", x => x.attribute_type_id);
                });

            migrationBuilder.CreateTable(
                name: "capability",
                columns: table => new
                {
                    capability_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capability", x => x.capability_id);
                });

            migrationBuilder.CreateTable(
                name: "configuration_audit_log",
                columns: table => new
                {
                    audit_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    initiated_by_admin_id = table.Column<int>(type: "integer", nullable: false),
                    approved_by_admin_id = table.Column<int>(type: "integer", nullable: false),
                    initiated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consequences_previewed = table.Column<string>(type: "text", nullable: false),
                    consequences_occurred = table.Column<string>(type: "text", nullable: false),
                    notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_audit_log", x => x.audit_id);
                    table.ForeignKey(
                        name: "fk_configuration_audit_log_admins_approved_by_admin_id",
                        column: x => x.approved_by_admin_id,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_configuration_audit_log_admins_initiated_by_admin_id",
                        column: x => x.initiated_by_admin_id,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entity_deletion_policy",
                columns: table => new
                {
                    policy_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requires_approval_for_delete = table.Column<bool>(type: "boolean", nullable: false),
                    requires_approval_for_deactivate = table.Column<bool>(type: "boolean", nullable: false),
                    required_delete_permission_tier_id = table.Column<int>(type: "integer", nullable: true),
                    required_deactivate_permission_tier_id = table.Column<int>(type: "integer", nullable: true),
                    requires_reason_for_delete = table.Column<bool>(type: "boolean", nullable: false),
                    requires_reason_for_deactivate = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_deletion_policy", x => x.policy_id);
                    table.ForeignKey(
                        name: "fk_entity_deletion_policy_permission_tier_required_deactivate_~",
                        column: x => x.required_deactivate_permission_tier_id,
                        principalTable: "permission_tier",
                        principalColumn: "permission_tier_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entity_deletion_policy_permission_tier_required_delete_perm~",
                        column: x => x.required_delete_permission_tier_id,
                        principalTable: "permission_tier",
                        principalColumn: "permission_tier_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_branch",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_branch", x => new { x.event_id, x.branch_id });
                    table.ForeignKey(
                        name: "fk_event_branch_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_branch_event_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_status",
                columns: table => new
                {
                    member_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_rosterable = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_status", x => x.member_status_id);
                });

            migrationBuilder.CreateTable(
                name: "notification_subscription",
                columns: table => new
                {
                    subscription_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_tier_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_admin_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_subscription", x => x.subscription_id);
                    table.CheckConstraint("chk_notification_subscription_recipient", "(recipient_tier_id IS NOT NULL AND recipient_admin_id IS NULL) OR (recipient_tier_id IS NULL AND recipient_admin_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_notification_subscription_admins_recipient_admin_id",
                        column: x => x.recipient_admin_id,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_subscription_permission_tier_recipient_tier_id",
                        column: x => x.recipient_tier_id,
                        principalTable: "permission_tier",
                        principalColumn: "permission_tier_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "readmission",
                columns: table => new
                {
                    readmission_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    readmission_date = table.Column<DateOnly>(type: "date", nullable: false),
                    performed_by_admin_id = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readmission", x => x.readmission_id);
                    table.ForeignKey(
                        name: "fk_readmission_admins_performed_by_admin_id",
                        column: x => x.performed_by_admin_id,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readmission_members_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member_attribute_value",
                columns: table => new
                {
                    member_attribute_value_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    attribute_type_id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_date = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_attribute_value", x => x.member_attribute_value_id);
                    table.ForeignKey(
                        name: "fk_member_attribute_value_admin_recorded_by_admin_admin_id",
                        column: x => x.recorded_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_member_attribute_value_attribute_type_attribute_type_id",
                        column: x => x.attribute_type_id,
                        principalTable: "attribute_type",
                        principalColumn: "attribute_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_member_attribute_value_member_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_rule",
                columns: table => new
                {
                    attendance_rule_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trigger_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trigger_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    outcome_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    outcome_status_id = table.Column<int>(type: "integer", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_rule", x => x.attendance_rule_id);
                    table.ForeignKey(
                        name: "fk_attendance_rule_member_statuses_outcome_status_id",
                        column: x => x.outcome_status_id,
                        principalTable: "member_status",
                        principalColumn: "member_status_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_rule_scope",
                columns: table => new
                {
                    attendance_rule_id = table.Column<int>(type: "integer", nullable: false),
                    criteria_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    criteria_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_rule_scope", x => new { x.attendance_rule_id, x.criteria_type, x.criteria_value });
                    table.ForeignKey(
                        name: "fk_attendance_rule_scope_attendance_rule_attendance_rule_id",
                        column: x => x.attendance_rule_id,
                        principalTable: "attendance_rule",
                        principalColumn: "attendance_rule_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_rule_enabled",
                table: "attendance_rule",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_rule_outcome_status_id",
                table: "attendance_rule",
                column: "outcome_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuration_audit_log_approved_by_admin_id",
                table: "configuration_audit_log",
                column: "approved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuration_audit_log_initiated_by_admin_id",
                table: "configuration_audit_log",
                column: "initiated_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_configuration_audit_log_notified_at",
                table: "configuration_audit_log",
                column: "notified_at");

            migrationBuilder.CreateIndex(
                name: "ix_entity_deletion_policy_required_deactivate_permission_tier_~",
                table: "entity_deletion_policy",
                column: "required_deactivate_permission_tier_id");

            migrationBuilder.CreateIndex(
                name: "ix_entity_deletion_policy_required_delete_permission_tier_id",
                table: "entity_deletion_policy",
                column: "required_delete_permission_tier_id");

            migrationBuilder.CreateIndex(
                name: "uq_entity_deletion_policy_entity_type",
                table: "entity_deletion_policy",
                column: "entity_type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_branch_branch_id",
                table: "event_branch",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_attribute_value_attribute_type_id",
                table: "member_attribute_value",
                column: "attribute_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_attribute_value_recorded_by",
                table: "member_attribute_value",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "uq_member_attribute_value_member_type",
                table: "member_attribute_value",
                columns: new[] { "member_id", "attribute_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_subscription_recipient_admin_id",
                table: "notification_subscription",
                column: "recipient_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_subscription_recipient_tier_id",
                table: "notification_subscription",
                column: "recipient_tier_id");

            migrationBuilder.CreateIndex(
                name: "ix_readmission_member",
                table: "readmission",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_readmission_performed_by_admin_id",
                table: "readmission",
                column: "performed_by_admin_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_rule_scope");

            migrationBuilder.DropTable(
                name: "capability");

            migrationBuilder.DropTable(
                name: "configuration_audit_log");

            migrationBuilder.DropTable(
                name: "entity_deletion_policy");

            migrationBuilder.DropTable(
                name: "event_branch");

            migrationBuilder.DropTable(
                name: "member_attribute_value");

            migrationBuilder.DropTable(
                name: "notification_subscription");

            migrationBuilder.DropTable(
                name: "readmission");

            migrationBuilder.DropTable(
                name: "attendance_rule");

            migrationBuilder.DropTable(
                name: "attribute_type");

            migrationBuilder.DropTable(
                name: "member_status");
        }
    }
}
