using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomConfigurationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "floor",
                schema: "lodging",
                table: "rooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "lodging",
                table: "rooms",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "lodging",
                table: "room_types",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "floor",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "lodging",
                table: "room_types");
        }
    }
}
