using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveHotel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tariffs");

            migrationBuilder.EnsureSchema(
                name: "lodging");

            migrationBuilder.CreateTable(
                name: "rate_plans",
                schema: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_plans", x => x.id);
                    table.UniqueConstraint("AK_rate_plans_code", x => x.code);
                    table.ForeignKey(
                        name: "FK_rate_plans_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_types",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_types", x => x.id);
                    table.UniqueConstraint("AK_room_types_hotel_unit_code_code", x => new { x.hotel_unit_code, x.code });
                    table.CheckConstraint("ck_room_types_capacity", "capacity > 0");
                    table.ForeignKey(
                        name: "FK_room_types_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_conventions",
                schema: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rate_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_conventions", x => x.id);
                    table.CheckConstraint("ck_customer_conventions_dates_ordered", "from_date <= to_date");
                    table.CheckConstraint("ck_customer_conventions_discount_percent_range", "discount_percent IS NULL OR (CAST(discount_percent AS numeric) >= 0 AND CAST(discount_percent AS numeric) <= 100)");
                    table.ForeignKey(
                        name: "FK_customer_conventions_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_customer_conventions_rate_plans_rate_plan_code",
                        column: x => x.rate_plan_code,
                        principalSchema: "tariffs",
                        principalTable: "rate_plans",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_periods",
                schema: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    nightly_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_periods", x => x.id);
                    table.CheckConstraint("ck_rate_periods_dates_ordered", "from_date <= to_date");
                    table.CheckConstraint("ck_rate_periods_nightly_amount_positive", "CAST(nightly_amount AS numeric) > 0");
                    table.ForeignKey(
                        name: "FK_rate_periods_rate_plans_rate_plan_id",
                        column: x => x.rate_plan_id,
                        principalSchema: "tariffs",
                        principalTable: "rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooms_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rooms_room_types_hotel_unit_code_room_type_code",
                        columns: x => new { x.hotel_unit_code, x.room_type_code },
                        principalSchema: "lodging",
                        principalTable: "room_types",
                        principalColumns: new[] { "hotel_unit_code", "code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    arrival_date = table.Column<DateOnly>(type: "date", nullable: false),
                    departure_date = table.Column<DateOnly>(type: "date", nullable: false),
                    guest_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nightly_rate_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rate_plan_code_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_out_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    no_show_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    no_show_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.id);
                    table.CheckConstraint("ck_reservations_dates", "departure_date > arrival_date");
                    table.CheckConstraint("ck_reservations_guest_count", "guest_count > 0");
                    table.CheckConstraint("ck_reservations_nightly_rate", "CAST(nightly_rate_snapshot AS numeric) >= 0");
                    table.CheckConstraint("ck_reservations_status", "status IN ('Booked', 'CheckedIn', 'CheckedOut', 'Cancelled', 'NoShow')");
                    table.ForeignKey(
                        name: "FK_reservations_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folios",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folios", x => x.id);
                    table.ForeignKey(
                        name: "FK_folios_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folio_charges",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    charge_date = table.Column<DateOnly>(type: "date", nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folio_charges", x => x.id);
                    table.CheckConstraint("ck_folio_charges_amount_nonzero", "CAST(amount AS numeric) <> 0");
                    table.CheckConstraint("ck_folio_charges_amount_sign", "kind IN ('Settlement', 'Adjustment') OR CAST(amount AS numeric) > 0");
                    table.CheckConstraint("ck_folio_charges_kind", "kind IN ('Night', 'Extra', 'Settlement', 'Adjustment')");
                    table.ForeignKey(
                        name: "FK_folio_charges_folios_folio_id",
                        column: x => x.folio_id,
                        principalSchema: "lodging",
                        principalTable: "folios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customer_conventions_customer_from_date",
                schema: "tariffs",
                table: "customer_conventions",
                columns: new[] { "customer_code", "from_date" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_conventions_rate_plan_code",
                schema: "tariffs",
                table: "customer_conventions",
                column: "rate_plan_code");

            migrationBuilder.CreateIndex(
                name: "ix_folio_charges_folio_id",
                schema: "lodging",
                table: "folio_charges",
                column: "folio_id");

            migrationBuilder.CreateIndex(
                name: "ux_folios_reservation_id",
                schema: "lodging",
                table: "folios",
                column: "reservation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_periods_plan_room_type_from_date",
                schema: "tariffs",
                table: "rate_periods",
                columns: new[] { "rate_plan_id", "room_type_code", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_rate_plans_hotel_unit_code",
                schema: "tariffs",
                table: "rate_plans",
                column: "hotel_unit_code",
                unique: true,
                filter: "is_default AND is_active");

            migrationBuilder.CreateIndex(
                name: "ux_rate_plans_code",
                schema: "tariffs",
                table: "rate_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservations_customer_code",
                schema: "lodging",
                table: "reservations",
                column: "customer_code");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_hotel_unit_code",
                schema: "lodging",
                table: "reservations",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_room_id_arrival_date",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "room_id", "arrival_date" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_status",
                schema: "lodging",
                table: "reservations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_room_types_hotel_unit_code",
                schema: "lodging",
                table: "room_types",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_hotel_unit_code",
                schema: "lodging",
                table: "rooms",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_hotel_unit_code_room_type_code",
                schema: "lodging",
                table: "rooms",
                columns: new[] { "hotel_unit_code", "room_type_code" });

            migrationBuilder.CreateIndex(
                name: "ux_rooms_hotel_unit_code_number",
                schema: "lodging",
                table: "rooms",
                columns: new[] { "hotel_unit_code", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_conventions",
                schema: "tariffs");

            migrationBuilder.DropTable(
                name: "folio_charges",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "rate_periods",
                schema: "tariffs");

            migrationBuilder.DropTable(
                name: "folios",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "rate_plans",
                schema: "tariffs");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "room_types",
                schema: "lodging");
        }
    }
}
