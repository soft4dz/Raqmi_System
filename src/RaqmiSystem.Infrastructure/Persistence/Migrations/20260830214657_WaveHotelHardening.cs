using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveHotelHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_rate_plans_hotel_unit_code",
                schema: "tariffs",
                table: "rate_plans",
                newName: "ux_rate_plans_default_per_unit");

            migrationBuilder.CreateIndex(
                name: "ix_rate_plans_hotel_unit_code",
                schema: "tariffs",
                table: "rate_plans",
                column: "hotel_unit_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rate_plans_hotel_unit_code",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.RenameIndex(
                name: "ux_rate_plans_default_per_unit",
                schema: "tariffs",
                table: "rate_plans",
                newName: "ix_rate_plans_hotel_unit_code");
        }
    }
}
