using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WavePmsFrontOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_guest_count",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_status",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ux_folios_reservation_id",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folio_charges_kind",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.AddColumn<string>(
                name: "amenities",
                schema: "lodging",
                table: "rooms",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "building",
                schema: "lodging",
                table: "rooms",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                schema: "lodging",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "internal_code",
                schema: "lodging",
                table: "rooms",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_accessible",
                schema: "lodging",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_smoking",
                schema: "lodging",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "view",
                schema: "lodging",
                table: "rooms",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wing",
                schema: "lodging",
                table: "rooms",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "amenities",
                schema: "lodging",
                table: "room_types",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_rate",
                schema: "lodging",
                table: "room_types",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_adults",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_children",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_infants",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "rank",
                schema: "lodging",
                table: "room_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "surface_square_meters",
                schema: "lodging",
                table: "room_types",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "room_id",
                schema: "lodging",
                table: "reservations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "adults",
                schema: "lodging",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "agency_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_fee_amount",
                schema: "lodging",
                table: "reservations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_policy_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_policy_snapshot",
                schema: "lodging",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "channel_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "children",
                schema: "lodging",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "company_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "convention_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "estimated_arrival_time",
                schema: "lodging",
                table: "reservations",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "estimated_departure_time",
                schema: "lodging",
                table: "reservations",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guarantee_kind",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "guarantee_reference",
                schema: "lodging",
                table: "reservations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "infants",
                schema: "lodging",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_overbooking",
                schema: "lodging",
                table: "reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_walk_in",
                schema: "lodging",
                table: "reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "market_segment_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "lodging",
                table: "reservations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "number",
                schema: "lodging",
                table: "reservations",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "original_room_type_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "room_type_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_code",
                schema: "lodging",
                table: "reservations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "special_requests",
                schema: "lodging",
                table: "reservations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "board",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_policy_code",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "channel_code",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "deposit_percent",
                schema: "tariffs",
                table: "rate_plans",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                schema: "tariffs",
                table: "rate_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_refundable",
                schema: "tariffs",
                table: "rate_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "market_segment_code",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "required_guarantee",
                schema: "tariffs",
                table: "rate_plans",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "valid_from",
                schema: "tariffs",
                table: "rate_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "valid_to",
                schema: "tariffs",
                table: "rate_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bill_to_customer_code",
                schema: "lodging",
                table: "folios",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_at",
                schema: "lodging",
                table: "folios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by",
                schema: "lodging",
                table: "folios",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hotel_unit_code",
                schema: "lodging",
                table: "folios",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "invoice_id",
                schema: "lodging",
                table: "folios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "lodging",
                table: "folios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "label",
                schema: "lodging",
                table: "folios",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "number",
                schema: "lodging",
                table: "folios",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "lodging",
                table: "folios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "business_date",
                schema: "lodging",
                table: "folio_charges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extra_code",
                schema: "lodging",
                table: "folio_charges",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                schema: "lodging",
                table: "folio_charges",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "source_reference",
                schema: "lodging",
                table: "folio_charges",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                schema: "lodging",
                table: "folio_charges",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cancellation_policies",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    no_show_basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    no_show_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cancellation_policies", x => x.id);
                    table.CheckConstraint("ck_cancellation_policies_no_show_basis", "no_show_basis IN ('None', 'FirstNight', 'Nights', 'PercentOfStay', 'FixedAmount')");
                    table.ForeignKey(
                        name: "FK_cancellation_policies_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deposits",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    paid_date = table.Column<DateOnly>(type: "date", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    applied_to_folio_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    refunded_date = table.Column<DateOnly>(type: "date", nullable: true),
                    closing_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deposits", x => x.id);
                    table.CheckConstraint("ck_deposits_amount", "CAST(amount AS numeric) > 0");
                    table.CheckConstraint("ck_deposits_status", "status IN ('Requested', 'Paid', 'Applied', 'Refunded', 'Forfeited')");
                    table.ForeignKey(
                        name: "FK_deposits_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "extra_items",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pricing_basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    charge_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_posted_by_night_audit = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extra_items", x => x.id);
                    table.CheckConstraint("ck_extra_items_charge_kind", "charge_kind IN ('Extra', 'Tax', 'Package')");
                    table.CheckConstraint("ck_extra_items_pricing_basis", "pricing_basis IN ('PerStay', 'PerNight', 'PerPerson', 'PerPersonPerNight', 'PerQuantity')");
                    table.CheckConstraint("ck_extra_items_unit_price", "CAST(unit_price AS numeric) >= 0");
                    table.CheckConstraint("ck_extra_items_vat_rate", "CAST(vat_rate AS numeric) IN (0, 9, 19)");
                    table.ForeignKey(
                        name: "FK_extra_items_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lodging_policies",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    check_in_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    check_out_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    early_check_in_from_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    early_check_in_is_free = table.Column<bool>(type: "boolean", nullable: false),
                    early_check_in_flat_charge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    early_check_in_percent_of_night = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    late_check_out_until_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    late_check_out_is_free = table.Column<bool>(type: "boolean", nullable: false),
                    late_check_out_flat_charge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    late_check_out_percent_of_night = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    out_of_service_reduces_inventory = table.Column<bool>(type: "boolean", nullable: false),
                    overbooking_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lodging_policies", x => x.id);
                    table.CheckConstraint("ck_lodging_policies_early_percent", "CAST(early_check_in_percent_of_night AS numeric) BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_lodging_policies_late_percent", "CAST(late_check_out_percent_of_night AS numeric) BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_lodging_policies_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "night_audit_runs",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    posted_room_nights = table.Column<int>(type: "integer", nullable: false),
                    posted_extras = table.Column<int>(type: "integer", nullable: false),
                    posted_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    no_shows_recorded = table.Column<int>(type: "integer", nullable: false),
                    skipped_already_posted = table.Column<int>(type: "integer", nullable: false),
                    pending_arrivals = table.Column<int>(type: "integer", nullable: false),
                    pending_departures = table.Column<int>(type: "integer", nullable: false),
                    open_folios = table.Column<int>(type: "integer", nullable: false),
                    room_state_mismatches = table.Column<int>(type: "integer", nullable: false),
                    report = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_night_audit_runs", x => x.id);
                    table.CheckConstraint("ck_night_audit_runs_status", "status IN ('Inspected', 'Completed', 'Blocked')");
                    table.ForeignKey(
                        name: "FK_night_audit_runs_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "overbooking_allowances",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    extra_rooms = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overbooking_allowances", x => x.id);
                    table.CheckConstraint("ck_overbooking_allowances_dates", "to_date >= from_date");
                    table.CheckConstraint("ck_overbooking_allowances_extra_rooms", "extra_rooms > 0 AND extra_rooms <= 50");
                    table.ForeignKey(
                        name: "FK_overbooking_allowances_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_overbooking_allowances_room_types_hotel_unit_code_room_type~",
                        columns: x => new { x.hotel_unit_code, x.room_type_code },
                        principalSchema: "lodging",
                        principalTable: "room_types",
                        principalColumns: new[] { "hotel_unit_code", "code" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "packages",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rate_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    nights = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packages", x => x.id);
                    table.CheckConstraint("ck_packages_total_price", "CAST(total_price AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_packages_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_restrictions",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    rate_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    channel_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    is_closed_to_arrival = table.Column<bool>(type: "boolean", nullable: false),
                    is_closed_to_departure = table.Column<bool>(type: "boolean", nullable: false),
                    minimum_stay = table.Column<int>(type: "integer", nullable: false),
                    maximum_stay = table.Column<int>(type: "integer", nullable: false),
                    min_advance_days = table.Column<int>(type: "integer", nullable: false),
                    max_advance_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_restrictions", x => x.id);
                    table.CheckConstraint("ck_rate_restrictions_advance_bounds", "min_advance_days >= 0 AND max_advance_days >= 0 AND (min_advance_days = 0 OR max_advance_days = 0 OR min_advance_days <= max_advance_days)");
                    table.CheckConstraint("ck_rate_restrictions_dates", "to_date >= from_date");
                    table.CheckConstraint("ck_rate_restrictions_stay_bounds", "minimum_stay >= 0 AND maximum_stay >= 0 AND (minimum_stay = 0 OR maximum_stay = 0 OR minimum_stay <= maximum_stay)");
                    table.ForeignKey(
                        name: "FK_rate_restrictions_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservation_events",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    previous_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    new_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_reservation_events_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reservation_extras",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extra_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label_snapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    pricing_basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate_snapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    charge_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_included_in_rate = table.Column<bool>(type: "boolean", nullable: false),
                    package_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_extras", x => x.id);
                    table.CheckConstraint("ck_reservation_extras_quantity", "CAST(quantity AS numeric) > 0");
                    table.CheckConstraint("ck_reservation_extras_unit_price", "CAST(unit_price_snapshot AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_reservation_extras_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_blocks",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_return_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    maintenance_reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_blocks", x => x.id);
                    table.CheckConstraint("ck_room_blocks_dates", "end_date > start_date");
                    table.CheckConstraint("ck_room_blocks_kind", "kind IN ('OutOfOrder', 'OutOfService')");
                    table.CheckConstraint("ck_room_blocks_status", "status IN ('Planned', 'Active', 'Closed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_room_blocks_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_blocks_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stay_room_assignments",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assigned_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stay_room_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_stay_room_assignments_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stay_room_assignments_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "lodging",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "yield_rules",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    room_type_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    rate_plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    trigger_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    threshold_value = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    days_of_week = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    adjustment_percent = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_yield_rules", x => x.id);
                    table.CheckConstraint("ck_yield_rules_adjustment", "CAST(adjustment_percent AS numeric) <> 0 AND CAST(adjustment_percent AS numeric) BETWEEN -300 AND 300");
                    table.CheckConstraint("ck_yield_rules_dates", "to_date >= from_date");
                    table.CheckConstraint("ck_yield_rules_trigger", "trigger_kind IN ('OccupancyAtOrAbove', 'OccupancyBelow', 'LeadTimeAtOrBelow', 'LeadTimeAtOrAbove', 'DayOfWeek', 'Always')");
                    table.ForeignKey(
                        name: "FK_yield_rules_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cancellation_policy_rules",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_days_before_arrival = table.Column<int>(type: "integer", nullable: false),
                    basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cancellation_policy_rules", x => x.id);
                    table.CheckConstraint("ck_cancellation_policy_rules_basis", "basis IN ('None', 'FirstNight', 'Nights', 'PercentOfStay', 'FixedAmount')");
                    table.CheckConstraint("ck_cancellation_policy_rules_days", "min_days_before_arrival >= 0 AND min_days_before_arrival <= 365");
                    table.ForeignKey(
                        name: "FK_cancellation_policy_rules_cancellation_policies_cancellatio~",
                        column: x => x.cancellation_policy_id,
                        principalSchema: "lodging",
                        principalTable: "cancellation_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "package_components",
                schema: "lodging",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    charge_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    extra_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    pricing_basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_components", x => x.id);
                    table.CheckConstraint("ck_package_components_amount", "CAST(amount AS numeric) > 0");
                    table.CheckConstraint("ck_package_components_charge_kind", "charge_kind IN ('Night', 'Extra', 'Tax', 'Package')");
                    table.ForeignKey(
                        name: "FK_package_components_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "lodging",
                        principalTable: "packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ============================ REPRISE DES DONNEES EXISTANTES ============================
            //
            // Les colonnes ci-dessus arrivent NOT NULL avec une valeur par defaut vide, et les index
            // uniques et contraintes de controle qui suivent les refuseraient telles quelles :
            //   - deux dossiers d'une meme unite partageraient le numero "" ;
            //   - un statut 'Booked' n'est plus dans la liste autorisee ;
            //   - guest_count = adults + children serait faux avec adults a zero ;
            //   - un folio sans unite ni numero heurterait sa propre cle unique et sa cle etrangere.
            //
            // Cette reprise s'execute donc AVANT la creation des index et des contraintes. Elle est
            // ecrite pour etre rejouable : chaque instruction ne touche que les lignes encore
            // vides, de sorte qu'une migration relancee apres un incident ne renumerote rien.

            // 1. Le statut Booked devient Confirmed. Le decoupage des statuts d'avant-arrivee a
            //    remplace un etat unique par quatre ; "confirme" est celui qui dit exactement ce
            //    que "Booked" disait : ferme, sans garantie financiere.
            migrationBuilder.Sql(
                "UPDATE lodging.reservations SET status = 'Confirmed' WHERE status = 'Booked';");

            // 2. La composition des occupants. L'ancien compteur unique devient un nombre
            //    d'ADULTES : c'est la lecture la plus prudente, un adulte occupant un couchage
            //    plein la ou un enfant peut n'en occuper aucun.
            migrationBuilder.Sql(
                "UPDATE lodging.reservations SET adults = guest_count, children = 0, infants = 0 "
                + "WHERE adults = 0;");

            // 3. La garantie : aucune n'a jamais ete saisie, donc aucune n'est declaree.
            migrationBuilder.Sql(
                "UPDATE lodging.reservations SET guarantee_kind = 'None' WHERE guarantee_kind = '';");

            // 4. Le type VENDU. Jusqu'ici une reservation portait une chambre et rien d'autre : le
            //    type se lisait a travers elle. On le fige donc depuis la chambre affectee, en type
            //    vendu ET en type d'origine - ces dossiers n'ont jamais ete surclasses.
            migrationBuilder.Sql(
                "UPDATE lodging.reservations AS r "
                + "SET room_type_code = rm.room_type_code, original_room_type_code = rm.room_type_code "
                + "FROM lodging.rooms AS rm "
                + "WHERE rm.id = r.room_id AND r.room_type_code = '';");

            // 5. Le numero de dossier, unique par unite. Il est reconstitue dans l'ordre de creation
            //    au format que le service produit desormais (R + annee sur deux chiffres + sequence
            //    sur six), de sorte que les dossiers repris et les nouveaux se lisent pareil.
            migrationBuilder.Sql(
                "WITH numbered AS ("
                + "SELECT id, 'R' || to_char(created_at, 'YY') || "
                + "lpad((row_number() OVER (PARTITION BY hotel_unit_code, to_char(created_at, 'YY') "
                + "ORDER BY created_at, id))::text, 6, '0') AS allocated "
                + "FROM lodging.reservations WHERE number = '') "
                + "UPDATE lodging.reservations AS r SET number = n.allocated "
                + "FROM numbered AS n WHERE n.id = r.id;");

            // 6. Les folios. Ils n'avaient ni unite, ni numero, ni nature : un sejour n'en portait
            //    qu'un. Ils deviennent le folio CLIENT du dossier, numerotes d'apres lui, et ceux
            //    des sejours deja soldes sont fermes - un compte ferme ne se reecrit plus.
            migrationBuilder.Sql(
                "UPDATE lodging.folios AS f "
                + "SET hotel_unit_code = r.hotel_unit_code, "
                + "number = r.number || '-1', "
                + "kind = 'Guest', "
                + "status = CASE WHEN r.status = 'CheckedOut' THEN 'Closed' ELSE 'Open' END "
                + "FROM lodging.reservations AS r "
                + "WHERE r.id = f.reservation_id AND f.hotel_unit_code = '';");

            migrationBuilder.CreateIndex(
                name: "ux_rooms_hotel_unit_code_internal_code",
                schema: "lodging",
                table: "rooms",
                columns: new[] { "hotel_unit_code", "internal_code" },
                unique: true,
                filter: "internal_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_unit_arrival_date",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "hotel_unit_code", "arrival_date" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_unit_departure_date",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "hotel_unit_code", "departure_date" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_unit_type_period",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "hotel_unit_code", "room_type_code", "arrival_date", "departure_date" });

            migrationBuilder.CreateIndex(
                name: "ux_reservations_hotel_unit_code_number",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "hotel_unit_code", "number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_cancellation_fee",
                schema: "lodging",
                table: "reservations",
                sql: "CAST(cancellation_fee_amount AS numeric) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_guest_count",
                schema: "lodging",
                table: "reservations",
                sql: "guest_count > 0 AND adults > 0 AND children >= 0 AND infants >= 0 AND guest_count = adults + children");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_status",
                schema: "lodging",
                table: "reservations",
                sql: "status IN ('Inquiry', 'Option', 'Confirmed', 'Guaranteed', 'CheckedIn', 'CheckedOut', 'Cancelled', 'NoShow')");

            migrationBuilder.CreateIndex(
                name: "ix_folios_reservation_id",
                schema: "lodging",
                table: "folios",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ux_folios_hotel_unit_code_number",
                schema: "lodging",
                table: "folios",
                columns: new[] { "hotel_unit_code", "number" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_folios_kind",
                schema: "lodging",
                table: "folios",
                sql: "kind IN ('Guest', 'Company', 'Agency', 'Group')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_folios_status",
                schema: "lodging",
                table: "folios",
                sql: "status IN ('Open', 'Closed')");

            migrationBuilder.CreateIndex(
                name: "ux_folio_charges_folio_id_source_reference",
                schema: "lodging",
                table: "folio_charges",
                columns: new[] { "folio_id", "source_reference" },
                unique: true,
                filter: "source_reference IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_folio_charges_kind",
                schema: "lodging",
                table: "folio_charges",
                sql: "kind IN ('Night', 'Extra', 'Settlement', 'Adjustment', 'Tax', 'Package')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_folio_charges_quantity",
                schema: "lodging",
                table: "folio_charges",
                sql: "CAST(quantity AS numeric) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_folio_charges_vat_rate",
                schema: "lodging",
                table: "folio_charges",
                sql: "vat_rate IS NULL OR CAST(vat_rate AS numeric) IN (0, 9, 19)");

            migrationBuilder.CreateIndex(
                name: "ux_cancellation_policies_hotel_unit_code_code",
                schema: "lodging",
                table: "cancellation_policies",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cancellation_policy_rules_policy_days",
                schema: "lodging",
                table: "cancellation_policy_rules",
                columns: new[] { "cancellation_policy_id", "min_days_before_arrival" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deposits_reservation_id",
                schema: "lodging",
                table: "deposits",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_deposits_status",
                schema: "lodging",
                table: "deposits",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_extra_items_hotel_unit_code_code",
                schema: "lodging",
                table: "extra_items",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_lodging_policies_hotel_unit_code",
                schema: "lodging",
                table: "lodging_policies",
                column: "hotel_unit_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_night_audit_runs_unit_business_date",
                schema: "lodging",
                table: "night_audit_runs",
                columns: new[] { "hotel_unit_code", "business_date" });

            migrationBuilder.CreateIndex(
                name: "ux_night_audit_runs_unit_business_date_completed",
                schema: "lodging",
                table: "night_audit_runs",
                columns: new[] { "hotel_unit_code", "business_date" },
                unique: true,
                filter: "status = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "ix_overbooking_allowances_unit_type_from",
                schema: "lodging",
                table: "overbooking_allowances",
                columns: new[] { "hotel_unit_code", "room_type_code", "from_date" });

            migrationBuilder.CreateIndex(
                name: "ix_package_components_package_id",
                schema: "lodging",
                table: "package_components",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ux_packages_hotel_unit_code_code",
                schema: "lodging",
                table: "packages",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_restrictions_unit_period",
                schema: "lodging",
                table: "rate_restrictions",
                columns: new[] { "hotel_unit_code", "from_date", "to_date" });

            migrationBuilder.CreateIndex(
                name: "ix_rate_restrictions_unit_room_type",
                schema: "lodging",
                table: "rate_restrictions",
                columns: new[] { "hotel_unit_code", "room_type_code" });

            migrationBuilder.CreateIndex(
                name: "ix_reservation_events_reservation_id_occurred_at",
                schema: "lodging",
                table: "reservation_events",
                columns: new[] { "reservation_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservation_extras_reservation_id",
                schema: "lodging",
                table: "reservation_extras",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_blocks_room_id_start_date",
                schema: "lodging",
                table: "room_blocks",
                columns: new[] { "room_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_room_blocks_status",
                schema: "lodging",
                table: "room_blocks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_room_blocks_unit_period",
                schema: "lodging",
                table: "room_blocks",
                columns: new[] { "hotel_unit_code", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_room_assignments_reservation_id_assigned_at",
                schema: "lodging",
                table: "stay_room_assignments",
                columns: new[] { "reservation_id", "assigned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stay_room_assignments_room_id",
                schema: "lodging",
                table: "stay_room_assignments",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_yield_rules_unit_period",
                schema: "lodging",
                table: "yield_rules",
                columns: new[] { "hotel_unit_code", "from_date", "to_date" });

            migrationBuilder.CreateIndex(
                name: "ux_yield_rules_hotel_unit_code_code",
                schema: "lodging",
                table: "yield_rules",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_folios_hotel_units_hotel_unit_code",
                schema: "lodging",
                table: "folios",
                column: "hotel_unit_code",
                principalSchema: "organization",
                principalTable: "hotel_units",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_room_types_hotel_unit_code_room_type_code",
                schema: "lodging",
                table: "reservations",
                columns: new[] { "hotel_unit_code", "room_type_code" },
                principalSchema: "lodging",
                principalTable: "room_types",
                principalColumns: new[] { "hotel_unit_code", "code" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_folios_hotel_units_hotel_unit_code",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropForeignKey(
                name: "FK_reservations_room_types_hotel_unit_code_room_type_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropTable(
                name: "cancellation_policy_rules",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "deposits",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "extra_items",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "lodging_policies",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "night_audit_runs",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "overbooking_allowances",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "package_components",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "rate_restrictions",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "reservation_events",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "reservation_extras",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "room_blocks",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "stay_room_assignments",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "yield_rules",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "cancellation_policies",
                schema: "lodging");

            migrationBuilder.DropTable(
                name: "packages",
                schema: "lodging");

            migrationBuilder.DropIndex(
                name: "ux_rooms_hotel_unit_code_internal_code",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_reservations_unit_arrival_date",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_unit_departure_date",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_unit_type_period",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ux_reservations_hotel_unit_code_number",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_cancellation_fee",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_guest_count",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_status",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_folios_reservation_id",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropIndex(
                name: "ux_folios_hotel_unit_code_number",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folios_kind",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folios_status",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropIndex(
                name: "ux_folio_charges_folio_id_source_reference",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folio_charges_kind",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folio_charges_quantity",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropCheckConstraint(
                name: "ck_folio_charges_vat_rate",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropColumn(
                name: "amenities",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "building",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "display_order",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "internal_code",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_accessible",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_smoking",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "view",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "wing",
                schema: "lodging",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "amenities",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "base_rate",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "display_order",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "max_adults",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "max_children",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "max_infants",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "rank",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "surface_square_meters",
                schema: "lodging",
                table: "room_types");

            migrationBuilder.DropColumn(
                name: "adults",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "agency_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_fee_amount",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_snapshot",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "channel_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "children",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "company_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "convention_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "estimated_arrival_time",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "estimated_departure_time",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "guarantee_kind",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "guarantee_reference",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "infants",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "is_overbooking",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "is_walk_in",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "market_segment_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "original_room_type_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "room_type_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "source_code",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "special_requests",
                schema: "lodging",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "board",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_code",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "channel_code",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "deposit_percent",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "display_order",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "is_refundable",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "market_segment_code",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "required_guarantee",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "valid_from",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "valid_to",
                schema: "tariffs",
                table: "rate_plans");

            migrationBuilder.DropColumn(
                name: "bill_to_customer_code",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "closed_at",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "closed_by",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "hotel_unit_code",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "invoice_id",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "label",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "lodging",
                table: "folios");

            migrationBuilder.DropColumn(
                name: "business_date",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropColumn(
                name: "extra_code",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropColumn(
                name: "source_reference",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                schema: "lodging",
                table: "folio_charges");

            migrationBuilder.AlterColumn<Guid>(
                name: "room_id",
                schema: "lodging",
                table: "reservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_guest_count",
                schema: "lodging",
                table: "reservations",
                sql: "guest_count > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_status",
                schema: "lodging",
                table: "reservations",
                sql: "status IN ('Booked', 'CheckedIn', 'CheckedOut', 'Cancelled', 'NoShow')");

            migrationBuilder.CreateIndex(
                name: "ux_folios_reservation_id",
                schema: "lodging",
                table: "folios",
                column: "reservation_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_folio_charges_kind",
                schema: "lodging",
                table: "folio_charges",
                sql: "kind IN ('Night', 'Extra', 'Settlement', 'Adjustment')");
        }
    }
}
