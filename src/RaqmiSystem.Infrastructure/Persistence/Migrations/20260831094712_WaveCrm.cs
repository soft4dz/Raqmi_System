using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "customer_segments",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_segments", x => x.id);
                    table.UniqueConstraint("AK_customer_segments_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "guest_interactions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    direction = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    handled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_interactions", x => x.id);
                    table.CheckConstraint("ck_guest_interactions_channel", "channel IN ('Phone', 'Email', 'Sms', 'InPerson', 'Web')");
                    table.CheckConstraint("ck_guest_interactions_direction", "direction IN ('Inbound', 'Outbound')");
                    table.ForeignKey(
                        name: "FK_guest_interactions_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_interactions_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_tiers",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    points_threshold = table.Column<int>(type: "integer", nullable: false),
                    benefits = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_tiers", x => x.id);
                    table.CheckConstraint("ck_loyalty_tiers_threshold", "points_threshold >= 0");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_transactions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_transactions", x => x.id);
                    table.CheckConstraint("ck_loyalty_transactions_sign", "(kind = 'Earn' AND points > 0) OR (kind IN ('Redeem', 'Expiry') AND points < 0) OR (kind = 'Adjustment' AND points <> 0)");
                    table.ForeignKey(
                        name: "FK_loyalty_transactions_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "satisfaction_entries",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    survey_date = table.Column<DateOnly>(type: "date", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_satisfaction_entries", x => x.id);
                    table.CheckConstraint("ck_satisfaction_entries_score", "score BETWEEN 0 AND 10");
                    table.CheckConstraint("ck_satisfaction_entries_source", "source IN ('InRoom', 'Email', 'FrontDesk', 'Online', 'Phone')");
                    table.ForeignKey(
                        name: "FK_satisfaction_entries_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_satisfaction_entries_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_satisfaction_entries_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "lodging",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_segment_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    objective = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    launched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    launched_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.id);
                    table.CheckConstraint("ck_campaigns_cancel_reason", "(status <> 'Cancelled') OR (cancel_reason IS NOT NULL)");
                    table.CheckConstraint("ck_campaigns_channel", "channel IN ('Email', 'Sms', 'Phone', 'OnSite')");
                    table.CheckConstraint("ck_campaigns_dates", "end_date >= start_date");
                    table.CheckConstraint("ck_campaigns_status", "status IN ('Draft', 'Scheduled', 'Running', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_campaigns_customer_segments_target_segment_code",
                        column: x => x.target_segment_code,
                        principalSchema: "crm",
                        principalTable: "customer_segments",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guest_profiles",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    segment_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    preferences = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_vip = table.Column<bool>(type: "boolean", nullable: false),
                    marketing_consent = table.Column<bool>(type: "boolean", nullable: false),
                    marketing_consent_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guest_profiles", x => x.id);
                    table.CheckConstraint("ck_guest_profiles_consent_stamp", "NOT marketing_consent OR marketing_consent_updated_at IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_guest_profiles_customer_segments_segment_code",
                        column: x => x.segment_code,
                        principalSchema: "crm",
                        principalTable: "customer_segments",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guest_profiles_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_status",
                schema: "crm",
                table: "campaigns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_target_segment_code",
                schema: "crm",
                table: "campaigns",
                column: "target_segment_code");

            migrationBuilder.CreateIndex(
                name: "ux_campaigns_code",
                schema: "crm",
                table: "campaigns",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guest_interactions_customer_code_occurred_at",
                schema: "crm",
                table: "guest_interactions",
                columns: new[] { "customer_code", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_guest_interactions_hotel_unit_code",
                schema: "crm",
                table: "guest_interactions",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_guest_profiles_segment_code",
                schema: "crm",
                table: "guest_profiles",
                column: "segment_code");

            migrationBuilder.CreateIndex(
                name: "ux_guest_profiles_customer_code",
                schema: "crm",
                table: "guest_profiles",
                column: "customer_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loyalty_tiers_code",
                schema: "crm",
                table: "loyalty_tiers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_loyalty_tiers_points_threshold_active",
                schema: "crm",
                table: "loyalty_tiers",
                column: "points_threshold",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_loyalty_transactions_customer_code_occurred_on",
                schema: "crm",
                table: "loyalty_transactions",
                columns: new[] { "customer_code", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_satisfaction_entries_customer_code",
                schema: "crm",
                table: "satisfaction_entries",
                column: "customer_code");

            migrationBuilder.CreateIndex(
                name: "ix_satisfaction_entries_hotel_unit_code_survey_date",
                schema: "crm",
                table: "satisfaction_entries",
                columns: new[] { "hotel_unit_code", "survey_date" });

            migrationBuilder.CreateIndex(
                name: "IX_satisfaction_entries_reservation_id",
                schema: "crm",
                table: "satisfaction_entries",
                column: "reservation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "guest_interactions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "guest_profiles",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "loyalty_tiers",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "loyalty_transactions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "satisfaction_entries",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_segments",
                schema: "crm");
        }
    }
}
