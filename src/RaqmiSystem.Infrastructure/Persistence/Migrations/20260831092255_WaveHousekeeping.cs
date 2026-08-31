using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveHousekeeping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "housekeeping");

            migrationBuilder.CreateTable(
                name: "housekeeping_tasks",
                schema: "housekeeping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    task_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    assigned_to = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cleaned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cleaned_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    inspected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    inspected_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    inspection_notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housekeeping_tasks", x => x.id);
                    table.CheckConstraint("ck_housekeeping_tasks_cancel_reason", "(status <> 'Cancelled') OR (cancel_reason IS NOT NULL)");
                    table.CheckConstraint("ck_housekeeping_tasks_duration", "duration_minutes IS NULL OR duration_minutes >= 0");
                    table.CheckConstraint("ck_housekeeping_tasks_rejection_reason", "(status <> 'Rejected') OR (inspection_notes IS NOT NULL)");
                    table.CheckConstraint("ck_housekeeping_tasks_status", "status IN ('Pending', 'InProgress', 'Cleaned', 'Inspected', 'Rejected', 'Cancelled')");
                    table.CheckConstraint("ck_housekeeping_tasks_task_type", "task_type IN ('Departure', 'Stayover', 'Vacant', 'DeepClean')");
                    table.ForeignKey(
                        name: "FK_housekeeping_tasks_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_housekeeping_tasks_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "minibar_consumptions",
                schema: "housekeeping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    consumed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_minibar_consumptions", x => x.id);
                    table.CheckConstraint("ck_minibar_consumptions_quantity", "quantity > 0");
                    table.CheckConstraint("ck_minibar_consumptions_total_amount", "CAST(total_amount AS numeric) > 0");
                    table.CheckConstraint("ck_minibar_consumptions_unit_price", "CAST(unit_price AS numeric) > 0");
                    table.ForeignKey(
                        name: "FK_minibar_consumptions_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_minibar_consumptions_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_minibar_consumptions_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "minibar_items",
                schema: "housekeeping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_minibar_items", x => x.id);
                    table.CheckConstraint("ck_minibar_items_unit_price", "CAST(unit_price AS numeric) > 0");
                    table.ForeignKey(
                        name: "FK_minibar_items_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_conditions",
                schema: "housekeeping",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_cleaned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_cleaned_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    last_inspected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_inspected_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    out_of_order_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    out_of_order_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_conditions", x => x.id);
                    table.CheckConstraint("ck_room_conditions_out_of_order_reason", "(status <> 'OutOfOrder') OR (out_of_order_reason IS NOT NULL)");
                    table.CheckConstraint("ck_room_conditions_status", "status IN ('Clean', 'Dirty', 'Inspected', 'OutOfOrder')");
                    table.ForeignKey(
                        name: "FK_room_conditions_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_conditions_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_housekeeping_tasks_assigned_to",
                schema: "housekeeping",
                table: "housekeeping_tasks",
                column: "assigned_to");

            migrationBuilder.CreateIndex(
                name: "ix_housekeeping_tasks_status",
                schema: "housekeeping",
                table: "housekeeping_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_housekeeping_tasks_unit_service_date",
                schema: "housekeeping",
                table: "housekeeping_tasks",
                columns: new[] { "hotel_unit_code", "service_date" });

            migrationBuilder.CreateIndex(
                name: "ux_housekeeping_tasks_room_date_type",
                schema: "housekeeping",
                table: "housekeeping_tasks",
                columns: new[] { "room_id", "service_date", "task_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_minibar_consumptions_reservation_id",
                schema: "housekeeping",
                table: "minibar_consumptions",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "IX_minibar_consumptions_room_id",
                schema: "housekeeping",
                table: "minibar_consumptions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_minibar_consumptions_unit_consumed_on",
                schema: "housekeeping",
                table: "minibar_consumptions",
                columns: new[] { "hotel_unit_code", "consumed_on" });

            migrationBuilder.CreateIndex(
                name: "ux_minibar_items_hotel_unit_code_code",
                schema: "housekeeping",
                table: "minibar_items",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_conditions_hotel_unit_code",
                schema: "housekeeping",
                table: "room_conditions",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ux_room_conditions_room_id",
                schema: "housekeeping",
                table: "room_conditions",
                column: "room_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "housekeeping_tasks",
                schema: "housekeeping");

            migrationBuilder.DropTable(
                name: "minibar_consumptions",
                schema: "housekeeping");

            migrationBuilder.DropTable(
                name: "minibar_items",
                schema: "housekeeping");

            migrationBuilder.DropTable(
                name: "room_conditions",
                schema: "housekeeping");
        }
    }
}
