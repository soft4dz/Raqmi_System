using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveKpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "kpi");

            migrationBuilder.CreateTable(
                name: "kpi_account_mappings",
                schema: "kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_prefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    group = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_account_mappings", x => x.id);
                    table.CheckConstraint("ck_kpi_account_mappings_group", "\"group\" IN ('Revenue', 'DepartmentalExpense', 'UndistributedExpense', 'FixedCharge', 'DepreciationAndProvision', 'FinancialResult', 'IncomeTax')");
                });

            migrationBuilder.CreateTable(
                name: "kpi_snapshots",
                schema: "kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    scope_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    granularity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    numerator = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    denominator = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: true),
                    quality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    formula_version = table.Column<int>(type: "integer", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_snapshots", x => x.id);
                    table.CheckConstraint("ck_kpi_snapshots_period", "period_end >= period_start");
                    table.CheckConstraint("ck_kpi_snapshots_quality", "quality IN ('Valid', 'Partial', 'MissingData', 'NotApplicable')");
                    table.CheckConstraint("ck_kpi_snapshots_status", "status IN ('Provisional', 'Closed')");
                    table.ForeignKey(
                        name: "FK_kpi_snapshots_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kpi_thresholds",
                schema: "kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    scope_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    favorable_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    critical_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    target_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    owner_role = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_thresholds", x => x.id);
                    table.ForeignKey(
                        name: "FK_kpi_thresholds_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_kpi_account_mappings_prefix",
                schema: "kpi",
                table: "kpi_account_mappings",
                column: "account_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_snapshots_hotel_unit_code",
                schema: "kpi",
                table: "kpi_snapshots",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_snapshots_period",
                schema: "kpi",
                table: "kpi_snapshots",
                columns: new[] { "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_kpi_snapshots_status",
                schema: "kpi",
                table: "kpi_snapshots",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_kpi_snapshots_code_scope_period",
                schema: "kpi",
                table: "kpi_snapshots",
                columns: new[] { "kpi_code", "scope_key", "period_start", "period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_kpi_thresholds_hotel_unit_code",
                schema: "kpi",
                table: "kpi_thresholds",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ux_kpi_thresholds_code_scope",
                schema: "kpi",
                table: "kpi_thresholds",
                columns: new[] { "kpi_code", "scope_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_account_mappings",
                schema: "kpi");

            migrationBuilder.DropTable(
                name: "kpi_snapshots",
                schema: "kpi");

            migrationBuilder.DropTable(
                name: "kpi_thresholds",
                schema: "kpi");
        }
    }
}
