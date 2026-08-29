using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "settings");

            migrationBuilder.AddColumn<string>(
                name: "issuer_address_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_ai_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_name_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_nif_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_nis_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issuer_rc_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_settings",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    singleton_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    company_nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    company_rc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    company_ai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    company_nis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    company_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    company_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    company_email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    default_vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    currency_label = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    audit_retention_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.id);
                    table.CheckConstraint("ck_application_settings_audit_retention_days", "audit_retention_days BETWEEN 30 AND 3650");
                    table.CheckConstraint("ck_application_settings_company_nif_length", "company_nif IS NULL OR length(company_nif) = 15");
                    table.CheckConstraint("ck_application_settings_default_vat_rate", "CAST(default_vat_rate AS numeric) IN (0, 9, 19)");
                    table.CheckConstraint("ck_application_settings_singleton", "singleton_key = 'GLOBAL'");
                });

            migrationBuilder.CreateIndex(
                name: "ux_application_settings_singleton_key",
                schema: "settings",
                table: "application_settings",
                column: "singleton_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_settings",
                schema: "settings");

            migrationBuilder.DropColumn(
                name: "issuer_address_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_ai_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_name_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_nif_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_nis_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "issuer_rc_snapshot",
                schema: "finance",
                table: "invoices");
        }
    }
}
