using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstablishmentSectorAndCrmDecoupling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_satisfaction_entries_reservations_reservation_id",
                schema: "crm",
                table: "satisfaction_entries");

            migrationBuilder.DropIndex(
                name: "IX_satisfaction_entries_reservation_id",
                schema: "crm",
                table: "satisfaction_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_hotel_units_unit_type",
                schema: "organization",
                table: "hotel_units");

            migrationBuilder.AddColumn<string>(
                name: "sector",
                schema: "organization",
                table: "hotel_units",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Hospitality");

            migrationBuilder.AddCheckConstraint(
                name: "ck_hotel_units_sector",
                schema: "organization",
                table: "hotel_units",
                sql: "sector IN ('Hospitality', 'Retail', 'Services', 'Manufacturing', 'Education', 'Health', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_hotel_units_unit_type",
                schema: "organization",
                table: "hotel_units",
                sql: "unit_type IN ('Hotel', 'Residence', 'BeachClub', 'Marina', 'Restaurant', 'Shop', 'Office', 'Warehouse', 'School', 'Clinic', 'Other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_hotel_units_sector",
                schema: "organization",
                table: "hotel_units");

            migrationBuilder.DropCheckConstraint(
                name: "ck_hotel_units_unit_type",
                schema: "organization",
                table: "hotel_units");

            migrationBuilder.DropColumn(
                name: "sector",
                schema: "organization",
                table: "hotel_units");

            migrationBuilder.CreateIndex(
                name: "IX_satisfaction_entries_reservation_id",
                schema: "crm",
                table: "satisfaction_entries",
                column: "reservation_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_hotel_units_unit_type",
                schema: "organization",
                table: "hotel_units",
                sql: "unit_type IN ('Hotel', 'Residence', 'BeachClub', 'Marina', 'Other')");

            migrationBuilder.AddForeignKey(
                name: "FK_satisfaction_entries_reservations_reservation_id",
                schema: "crm",
                table: "satisfaction_entries",
                column: "reservation_id",
                principalSchema: "lodging",
                principalTable: "reservations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
