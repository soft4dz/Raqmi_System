using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveMiceEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_bookings",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reference = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    function_space_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    customer_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    setup_minutes = table.Column<int>(type: "integer", nullable: false),
                    teardown_minutes = table.Column<int>(type: "integer", nullable: false),
                    occupied_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    occupied_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    setup_style = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    expected_attendance = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_bookings", x => x.id);
                    table.CheckConstraint("ck_event_bookings_attendance", "expected_attendance > 0");
                    table.CheckConstraint("ck_event_bookings_buffers", "setup_minutes >= 0 AND teardown_minutes >= 0");
                    table.CheckConstraint("ck_event_bookings_duration", "duration_minutes > 0");
                    table.CheckConstraint("ck_event_bookings_window", "occupied_to > occupied_from");
                });

            migrationBuilder.CreateTable(
                name: "function_spaces",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    max_attendance = table.Column<int>(type: "integer", nullable: false),
                    area_square_meters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_function_spaces", x => x.id);
                    table.CheckConstraint("ck_function_spaces_area", "area_square_meters IS NULL OR CAST(area_square_meters AS numeric) > 0");
                    table.CheckConstraint("ck_function_spaces_capacity", "max_attendance > 0");
                });

            migrationBuilder.CreateTable(
                name: "event_booking_lines",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_booking_lines", x => x.id);
                    table.CheckConstraint("ck_event_booking_lines_quantity", "CAST(quantity AS numeric) > 0");
                    table.CheckConstraint("ck_event_booking_lines_unit_price", "CAST(unit_price AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_event_booking_lines_event_bookings_event_booking_id",
                        column: x => x.event_booking_id,
                        principalSchema: "lodging",
                        principalTable: "event_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_schedule_items",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    department = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_schedule_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_schedule_items_event_bookings_event_booking_id",
                        column: x => x.event_booking_id,
                        principalSchema: "lodging",
                        principalTable: "event_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_booking_lines_booking",
                schema: "lodging",
                table: "event_booking_lines",
                column: "event_booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_bookings_space_window",
                schema: "lodging",
                table: "event_bookings",
                columns: new[] { "hotel_unit_code", "function_space_code", "occupied_from" });

            migrationBuilder.CreateIndex(
                name: "ux_event_bookings_unit_reference",
                schema: "lodging",
                table: "event_bookings",
                columns: new[] { "hotel_unit_code", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_schedule_items_booking",
                schema: "lodging",
                table: "event_schedule_items",
                column: "event_booking_id");

            migrationBuilder.CreateIndex(
                name: "ux_function_spaces_unit_code",
                schema: "lodging",
                table: "function_spaces",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_booking_lines",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "event_schedule_items",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "function_spaces",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "event_bookings",
                schema: "lodging");
        }
    }
}
