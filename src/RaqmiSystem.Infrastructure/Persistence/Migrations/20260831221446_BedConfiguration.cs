using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BedConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_cots",
                schema: "lodging",
                table: "rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_extra_beds",
                schema: "lodging",
                table: "rooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_cots",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_extra_beds",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "room_beds",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_beds", x => x.id);
                    table.CheckConstraint("ck_room_beds_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_room_beds_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_type_beds",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_type_beds", x => x.id);
                    table.CheckConstraint("ck_room_type_beds_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_room_type_beds_room_types_room_type_id",
                        column: x => x.room_type_id,
                        principalSchema: "lodging",
                        principalTable: "room_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_room_beds_room_bed",
                schema: "lodging",
                table: "room_beds",
                columns: new[] { "room_id", "bed_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_room_type_beds_type_bed",
                schema: "lodging",
                table: "room_type_beds",
                columns: new[] { "room_type_id", "bed_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_beds",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "room_type_beds",
                schema: "lodging");

            migrationBuilder.DropColumn(
                name: "max_cots",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "max_extra_beds",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "max_cots",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "max_extra_beds",
                schema: "lodging",
                table: "room_types");
        }
    }
}
