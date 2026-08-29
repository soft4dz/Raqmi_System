using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1WaveOneHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customer_address_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_ai_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_name_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_nif_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_nis_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_rc_snapshot",
                schema: "finance",
                table: "invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customer_address_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_ai_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_name_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_nif_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_nis_snapshot",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "customer_rc_snapshot",
                schema: "finance",
                table: "invoices");
        }
    }
}
