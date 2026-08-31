using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveApprovalsReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approvals");

            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "approval_circuits",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_circuits", x => x.id);
                    table.CheckConstraint("ck_approval_circuits_subject_type", "subject_type IN ('PaymentOrder')");
                });

            migrationBuilder.CreateTable(
                name: "approval_instances",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    circuit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    circuit_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_instances", x => x.id);
                    table.CheckConstraint("ck_approval_instances_status", "status IN ('InProgress', 'Approved', 'Rejected')");
                    table.CheckConstraint("ck_approval_instances_subject_type", "subject_type IN ('PaymentOrder')");
                });

            migrationBuilder.CreateTable(
                name: "report_executions",
                schema: "reporting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    parameters_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    duration_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_executions", x => x.id);
                    table.CheckConstraint("ck_report_executions_duration", "duration_milliseconds >= 0");
                    table.CheckConstraint("ck_report_executions_row_count", "row_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "approval_steps",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    circuit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    required_role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_steps", x => x.id);
                    table.CheckConstraint("ck_approval_steps_rank_positive", "rank >= 1");
                    table.ForeignKey(
                        name: "FK_approval_steps_approval_circuits_circuit_id",
                        column: x => x.circuit_id,
                        principalSchema: "approvals",
                        principalTable: "approval_circuits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    step_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    decided_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    approved = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_decisions", x => x.id);
                    table.CheckConstraint("ck_approval_decisions_rank_positive", "rank >= 1");
                    table.ForeignKey(
                        name: "FK_approval_decisions_approval_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "approvals",
                        principalTable: "approval_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "approval_instance_steps",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    required_role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_instance_steps", x => x.id);
                    table.CheckConstraint("ck_approval_instance_steps_rank_positive", "rank >= 1");
                    table.ForeignKey(
                        name: "FK_approval_instance_steps_approval_instances_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "approvals",
                        principalTable: "approval_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_circuits_subject_type",
                schema: "approvals",
                table: "approval_circuits",
                column: "subject_type");

            migrationBuilder.CreateIndex(
                name: "ux_approval_circuits_code",
                schema: "approvals",
                table: "approval_circuits",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_instance_id",
                schema: "approvals",
                table: "approval_decisions",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ux_approval_decisions_instance_rank",
                schema: "approvals",
                table: "approval_decisions",
                columns: new[] { "instance_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_steps_instance_id",
                schema: "approvals",
                table: "approval_instance_steps",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ux_approval_instance_steps_instance_rank",
                schema: "approvals",
                table: "approval_instance_steps",
                columns: new[] { "instance_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_instances_status",
                schema: "approvals",
                table: "approval_instances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_approval_instances_subject",
                schema: "approvals",
                table: "approval_instances",
                columns: new[] { "subject_type", "subject_reference" });

            migrationBuilder.CreateIndex(
                name: "ux_approval_instances_open_subject",
                schema: "approvals",
                table: "approval_instances",
                columns: new[] { "subject_type", "subject_reference" },
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "ix_approval_steps_circuit_id",
                schema: "approvals",
                table: "approval_steps",
                column: "circuit_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_executions_created_at",
                schema: "reporting",
                table: "report_executions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_report_executions_report_code",
                schema: "reporting",
                table: "report_executions",
                column: "report_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_decisions",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "approval_instance_steps",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "approval_steps",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "report_executions",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "approval_instances",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "approval_circuits",
                schema: "approvals");
        }
    }
}
