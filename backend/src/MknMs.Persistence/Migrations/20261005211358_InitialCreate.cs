using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MknMs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignment_status",
                columns: table => new
                {
                    assignment_status_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_status", x => x.assignment_status_id);
                });

            migrationBuilder.CreateTable(
                name: "branch",
                columns: table => new
                {
                    branch_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    location = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_branch", x => x.branch_id);
                });

            migrationBuilder.CreateTable(
                name: "duty",
                columns: table => new
                {
                    duty_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duty", x => x.duty_id);
                });

            migrationBuilder.CreateTable(
                name: "event",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "outcome_state",
                columns: table => new
                {
                    outcome_state_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outcome_state", x => x.outcome_state_id);
                });

            migrationBuilder.CreateTable(
                name: "permission_tier",
                columns: table => new
                {
                    permission_tier_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_tier", x => x.permission_tier_id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "service_type",
                columns: table => new
                {
                    service_type_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_type", x => x.service_type_id);
                });

            migrationBuilder.CreateTable(
                name: "system_setting",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_setting", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "time_of_day",
                columns: table => new
                {
                    time_of_day_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_of_day", x => x.time_of_day_id);
                });

            migrationBuilder.CreateTable(
                name: "duty_rule",
                columns: table => new
                {
                    rule_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    duty_id = table.Column<int>(type: "integer", nullable: false),
                    tier_order = table.Column<int>(type: "integer", nullable: false),
                    criteria_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    criteria_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_duty_rule", x => x.rule_id);
                    table.ForeignKey(
                        name: "fk_duty_rule_duty_duty_id",
                        column: x => x.duty_id,
                        principalTable: "duty",
                        principalColumn: "duty_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program",
                columns: table => new
                {
                    program_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program", x => x.program_id);
                    table.ForeignKey(
                        name: "fk_program_event_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "member",
                columns: table => new
                {
                    member_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    join_date = table.Column<DateOnly>(type: "date", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    membership_stage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    surname = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    gender = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member", x => x.member_id);
                    table.ForeignKey(
                        name: "fk_member_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_member_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_definition",
                columns: table => new
                {
                    service_def_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_type_id = table.Column<int>(type: "integer", nullable: false),
                    owning_branch_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_definition", x => x.service_def_id);
                    table.ForeignKey(
                        name: "fk_service_definition_branch_owning_branch_id",
                        column: x => x.owning_branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_definition_service_type_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_type",
                        principalColumn: "service_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "branch_time_slot",
                columns: table => new
                {
                    time_slot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    time_of_day_id = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_time_slot", x => x.time_slot_id);
                    table.ForeignKey(
                        name: "fk_branch_time_slot_branch_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branch",
                        principalColumn: "branch_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_branch_time_slot_time_of_day_time_of_day_id",
                        column: x => x.time_of_day_id,
                        principalTable: "time_of_day",
                        principalColumn: "time_of_day_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin",
                columns: table => new
                {
                    admin_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    permission_tier_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin", x => x.admin_id);
                    table.ForeignKey(
                        name: "fk_admin_members_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_admin_permission_tier_permission_tier_id",
                        column: x => x.permission_tier_id,
                        principalTable: "permission_tier",
                        principalColumn: "permission_tier_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_item",
                columns: table => new
                {
                    item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    program_id = table.Column<int>(type: "integer", nullable: false),
                    sequence_order = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    service_def_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_item", x => x.item_id);
                    table.ForeignKey(
                        name: "fk_program_item_program_program_id",
                        column: x => x.program_id,
                        principalTable: "program",
                        principalColumn: "program_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_program_item_service_definition_service_definition_temp_id",
                        column: x => x.service_def_id,
                        principalTable: "service_definition",
                        principalColumn: "service_def_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_definition_duty",
                columns: table => new
                {
                    service_def_id = table.Column<int>(type: "integer", nullable: false),
                    duty_id = table.Column<int>(type: "integer", nullable: false),
                    required_slot_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_definition_duty", x => new { x.service_def_id, x.duty_id });
                    table.ForeignKey(
                        name: "fk_service_definition_duty_duty_duty_id",
                        column: x => x.duty_id,
                        principalTable: "duty",
                        principalColumn: "duty_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_definition_duty_service_definition_service_definiti~",
                        column: x => x.service_def_id,
                        principalTable: "service_definition",
                        principalColumn: "service_def_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_schedule",
                columns: table => new
                {
                    schedule_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_def_id = table.Column<int>(type: "integer", nullable: false),
                    time_slot_id = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_schedule", x => x.schedule_id);
                    table.ForeignKey(
                        name: "fk_service_schedule_branch_time_slot_time_slot_id",
                        column: x => x.time_slot_id,
                        principalTable: "branch_time_slot",
                        principalColumn: "time_slot_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_schedule_service_definition_service_definition_temp~",
                        column: x => x.service_def_id,
                        principalTable: "service_definition",
                        principalColumn: "service_def_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "eligibility",
                columns: table => new
                {
                    eligibility_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    duty_id = table.Column<int>(type: "integer", nullable: false),
                    granted_date = table.Column<DateOnly>(type: "date", nullable: false),
                    granted_by = table.Column<int>(type: "integer", nullable: false),
                    revoked_date = table.Column<DateOnly>(type: "date", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eligibility", x => x.eligibility_id);
                    table.ForeignKey(
                        name: "fk_eligibility_admin_granted_by_admin_admin_id",
                        column: x => x.granted_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eligibility_duty_duty_id",
                        column: x => x.duty_id,
                        principalTable: "duty",
                        principalColumn: "duty_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_eligibility_member_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identifier_history",
                columns: table => new
                {
                    entry_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    assigned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    unassigned_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    authorized_by = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identifier_history", x => x.entry_id);
                    table.ForeignKey(
                        name: "fk_identifier_history_admin_authorized_by_admin_admin_id",
                        column: x => x.authorized_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_identifier_history_member_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materializer_run",
                columns: table => new
                {
                    run_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trigger_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    triggered_by = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    schedules_evaluated = table.Column<int>(type: "integer", nullable: false),
                    occurrences_created = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materializer_run", x => x.run_id);
                    table.CheckConstraint("chk_materializer_run_status", "(status = 'Success' AND completed_at IS NOT NULL) OR (status = 'Failure' AND error_detail IS NOT NULL)");
                    table.CheckConstraint("chk_materializer_run_status_value", "status IN ('Success', 'Failure')");
                    table.CheckConstraint("chk_materializer_run_trigger", "(trigger_type = 'Manual' AND triggered_by IS NOT NULL) OR (trigger_type = 'Scheduled' AND triggered_by IS NULL)");
                    table.CheckConstraint("chk_materializer_run_trigger_type", "trigger_type IN ('Scheduled', 'Manual')");
                    table.ForeignKey(
                        name: "fk_materializer_run_admins_triggered_by_admin_admin_id",
                        column: x => x.triggered_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_duty",
                columns: table => new
                {
                    event_duty_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    program_item_id = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assigned_member_id = table.Column<int>(type: "integer", nullable: false),
                    assignment_status_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_duty", x => x.event_duty_id);
                    table.ForeignKey(
                        name: "fk_event_duty_assignment_status_assignment_status_id",
                        column: x => x.assignment_status_id,
                        principalTable: "assignment_status",
                        principalColumn: "assignment_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_duty_member_assigned_member_id",
                        column: x => x.assigned_member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_duty_program_items_program_item_id",
                        column: x => x.program_item_id,
                        principalTable: "program_item",
                        principalColumn: "item_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_occurrence",
                columns: table => new
                {
                    occurrence_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    schedule_id = table.Column<int>(type: "integer", nullable: true),
                    event_id = table.Column<int>(type: "integer", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_type_id = table.Column<int>(type: "integer", nullable: true),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    fill_status_id = table.Column<int>(type: "integer", nullable: true),
                    generated_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    changed_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_occurrence", x => x.occurrence_id);
                    table.CheckConstraint("chk_service_occurrence_generated_by", "generated_by IN ('System', 'Administrator')");
                    table.CheckConstraint("chk_service_occurrence_provenance", "(generated_by = 'System' AND created_by IS NULL) OR (generated_by = 'Administrator' AND created_by IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_service_occurrence_admins_changed_by_admin_admin_id",
                        column: x => x.changed_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_admins_created_by_admin_admin_id",
                        column: x => x.created_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_events_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_outcome_state_fill_status_id",
                        column: x => x.fill_status_id,
                        principalTable: "outcome_state",
                        principalColumn: "outcome_state_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_service_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "service_schedule",
                        principalColumn: "schedule_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_service_type_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_type",
                        principalColumn: "service_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_record",
                columns: table => new
                {
                    record_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    occurrence_id = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_record", x => x.record_id);
                    table.ForeignKey(
                        name: "fk_attendance_record_member_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_record_service_occurrence_service_occurrence_tem~",
                        column: x => x.occurrence_id,
                        principalTable: "service_occurrence",
                        principalColumn: "occurrence_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "roster_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    duty_id = table.Column<int>(type: "integer", nullable: false),
                    occurrence_id = table.Column<int>(type: "integer", nullable: false),
                    assignment_status_id = table.Column<int>(type: "integer", nullable: true),
                    approved_by = table.Column<int>(type: "integer", nullable: true),
                    assignment_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_by = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roster_assignment", x => x.assignment_id);
                    table.CheckConstraint("chk_roster_assignment_source", "assignment_source IN ('Automatic', 'Manual')");
                    table.ForeignKey(
                        name: "fk_roster_assignment_admin_approved_by_admin_admin_id",
                        column: x => x.approved_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roster_assignment_admin_assigned_by_admin_admin_id",
                        column: x => x.assigned_by,
                        principalTable: "admin",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roster_assignment_assignment_status_assignment_status_id",
                        column: x => x.assignment_status_id,
                        principalTable: "assignment_status",
                        principalColumn: "assignment_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roster_assignment_duty_duty_id",
                        column: x => x.duty_id,
                        principalTable: "duty",
                        principalColumn: "duty_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roster_assignment_member_member_id",
                        column: x => x.member_id,
                        principalTable: "member",
                        principalColumn: "member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_roster_assignment_service_occurrence_service_occurrence_tem~",
                        column: x => x.occurrence_id,
                        principalTable: "service_occurrence",
                        principalColumn: "occurrence_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_occurrence_duty",
                columns: table => new
                {
                    occurrence_id = table.Column<int>(type: "integer", nullable: false),
                    duty_id = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    required_slot_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_occurrence_duty", x => new { x.occurrence_id, x.duty_id });
                    table.CheckConstraint("chk_service_occurrence_duty_action", "action IN ('Added', 'Removed')");
                    table.ForeignKey(
                        name: "fk_service_occurrence_duty_duty_duty_id",
                        column: x => x.duty_id,
                        principalTable: "duty",
                        principalColumn: "duty_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_occurrence_duty_service_occurrence_service_occurren~",
                        column: x => x.occurrence_id,
                        principalTable: "service_occurrence",
                        principalColumn: "occurrence_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_member",
                table: "admin",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_admin_tier",
                table: "admin",
                column: "permission_tier_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_record_member_ts",
                table: "attendance_record",
                columns: new[] { "member_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_record_occurrence",
                table: "attendance_record",
                column: "occurrence_id");

            migrationBuilder.CreateIndex(
                name: "uq_attendance_record_member_occurrence",
                table: "attendance_record",
                columns: new[] { "member_id", "occurrence_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_branch_time_slot_branch",
                table: "branch_time_slot",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_branch_time_slot_time_of_day_id",
                table: "branch_time_slot",
                column: "time_of_day_id");

            migrationBuilder.CreateIndex(
                name: "ix_duty_rule_duty_tier",
                table: "duty_rule",
                columns: new[] { "duty_id", "tier_order" });

            migrationBuilder.CreateIndex(
                name: "ix_eligibility_duty_id",
                table: "eligibility",
                column: "duty_id");

            migrationBuilder.CreateIndex(
                name: "IX_eligibility_granted_by",
                table: "eligibility",
                column: "granted_by");

            migrationBuilder.CreateIndex(
                name: "ix_eligibility_member_duty",
                table: "eligibility",
                columns: new[] { "member_id", "duty_id" });

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_assigned_member",
                table: "event_duty",
                column: "assigned_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_assignment_status_id",
                table: "event_duty",
                column: "assignment_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_duty_program_item",
                table: "event_duty",
                column: "program_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_identifier_history_authorized_by",
                table: "identifier_history",
                column: "authorized_by");

            migrationBuilder.CreateIndex(
                name: "ix_identifier_history_member",
                table: "identifier_history",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "ix_materializer_run_started_at",
                table: "materializer_run",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_materializer_run_triggered_by",
                table: "materializer_run",
                column: "triggered_by");

            migrationBuilder.CreateIndex(
                name: "ix_member_branch",
                table: "member",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_role",
                table: "member",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_program_event",
                table: "program",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_program_item_program",
                table: "program_item",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_item_service_def",
                table: "program_item",
                column: "service_def_id");

            migrationBuilder.CreateIndex(
                name: "IX_roster_assignment_approved_by",
                table: "roster_assignment",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_roster_assignment_assigned_by",
                table: "roster_assignment",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "ix_roster_assignment_assignment_status_id",
                table: "roster_assignment",
                column: "assignment_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_roster_assignment_created_at",
                table: "roster_assignment",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_roster_assignment_duty_id",
                table: "roster_assignment",
                column: "duty_id");

            migrationBuilder.CreateIndex(
                name: "ix_roster_assignment_occurrence_duty",
                table: "roster_assignment",
                columns: new[] { "occurrence_id", "duty_id" });

            migrationBuilder.CreateIndex(
                name: "uq_roster_assignment_member_duty_occurrence",
                table: "roster_assignment",
                columns: new[] { "member_id", "duty_id", "occurrence_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_definition_owning_branch",
                table: "service_definition",
                column: "owning_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_definition_service_type",
                table: "service_definition",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_definition_duty_duty",
                table: "service_definition_duty",
                column: "duty_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_occurrence_changed_by",
                table: "service_occurrence",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "IX_service_occurrence_created_by",
                table: "service_occurrence",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_service_occurrence_date",
                table: "service_occurrence",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_service_occurrence_event_id",
                table: "service_occurrence",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_occurrence_fill_status_id",
                table: "service_occurrence",
                column: "fill_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_occurrence_service_type_id",
                table: "service_occurrence",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_occurrence_schedule_date",
                table: "service_occurrence",
                columns: new[] { "schedule_id", "date" },
                unique: true,
                filter: "schedule_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_occurrence_duty_duty",
                table: "service_occurrence_duty",
                column: "duty_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_schedule_service_def",
                table: "service_schedule",
                column: "service_def_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_schedule_time_slot",
                table: "service_schedule",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "uq_service_schedule_active",
                table: "service_schedule",
                columns: new[] { "service_def_id", "time_slot_id" },
                unique: true,
                filter: "is_active = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_record");

            migrationBuilder.DropTable(
                name: "duty_rule");

            migrationBuilder.DropTable(
                name: "eligibility");

            migrationBuilder.DropTable(
                name: "event_duty");

            migrationBuilder.DropTable(
                name: "identifier_history");

            migrationBuilder.DropTable(
                name: "materializer_run");

            migrationBuilder.DropTable(
                name: "roster_assignment");

            migrationBuilder.DropTable(
                name: "service_definition_duty");

            migrationBuilder.DropTable(
                name: "service_occurrence_duty");

            migrationBuilder.DropTable(
                name: "system_setting");

            migrationBuilder.DropTable(
                name: "program_item");

            migrationBuilder.DropTable(
                name: "assignment_status");

            migrationBuilder.DropTable(
                name: "duty");

            migrationBuilder.DropTable(
                name: "service_occurrence");

            migrationBuilder.DropTable(
                name: "program");

            migrationBuilder.DropTable(
                name: "admin");

            migrationBuilder.DropTable(
                name: "outcome_state");

            migrationBuilder.DropTable(
                name: "service_schedule");

            migrationBuilder.DropTable(
                name: "event");

            migrationBuilder.DropTable(
                name: "member");

            migrationBuilder.DropTable(
                name: "permission_tier");

            migrationBuilder.DropTable(
                name: "branch_time_slot");

            migrationBuilder.DropTable(
                name: "service_definition");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "time_of_day");

            migrationBuilder.DropTable(
                name: "branch");

            migrationBuilder.DropTable(
                name: "service_type");
        }
    }
}
