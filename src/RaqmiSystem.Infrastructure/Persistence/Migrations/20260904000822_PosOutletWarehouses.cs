using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PosOutletWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "warehouse_code",
                schema: "pos",
                table: "outlets",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_outlets_warehouse_code",
                schema: "pos",
                table: "outlets",
                column: "warehouse_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_outlets_warehouses_warehouse_code",
                schema: "pos",
                table: "outlets",
                column: "warehouse_code",
                principalSchema: "inventory",
                principalTable: "warehouses",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_outlets_warehouses_warehouse_code",
                schema: "pos",
                table: "outlets");

            migrationBuilder.DropIndex(
                name: "IX_outlets_warehouse_code",
                schema: "pos",
                table: "outlets");

            migrationBuilder.DropColumn(
                name: "warehouse_code",
                schema: "pos",
                table: "outlets");
        }
    }
}
