using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomAllotments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "allotment_id",
                schema: "lodging",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guest_name",
                schema: "lodging",
                table: "reservations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "room_allotments",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    customer_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    arrival_date = table.Column<DateOnly>(type: "date", nullable: false),
                    departure_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rooms_held = table.Column<int>(type: "integer", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_allotments", x => x.id);
                    table.CheckConstraint("ck_room_allotments_period", "departure_date > arrival_date");
                    table.CheckConstraint("ck_room_allotments_release", "release_date IS NULL OR release_date <= arrival_date");
                    table.CheckConstraint("ck_room_allotments_rooms", "rooms_held > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_allotment_id",
                schema: "lodging",
                table: "reservations",
                column: "allotment_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_allotments_unit_type_period",
                schema: "lodging",
                table: "room_allotments",
                columns: new[] { "hotel_unit_code", "room_type_code", "arrival_date" });

            migrationBuilder.CreateIndex(
                name: "ux_room_allotments_unit_reference",
                schema: "lodging",
                table: "room_allotments",
                columns: new[] { "hotel_unit_code", "reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_allotments",
                schema: "lodging");

            migrationBuilder.DropIndex(
                name: "ix_reservations_allotment_id",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "allotment_id",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "guest_name",
                schema: "lodging",
                table: "reservations");
        }
    }
}
