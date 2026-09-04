using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WavePointOfSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pos");

            migrationBuilder.CreateTable(
                name: "outlets",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outlets", x => x.id);
                    table.ForeignKey(
                        name: "FK_outlets_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_outlets_outlet_id",
                        column: x => x.outlet_id,
                        principalSchema: "pos",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tables",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tables", x => x.id);
                    table.ForeignKey(
                        name: "FK_tables_outlets_outlet_id",
                        column: x => x.outlet_id,
                        principalSchema: "pos",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    order_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_tickets_outlets_outlet_id",
                        column: x => x.outlet_id,
                        principalSchema: "pos",
                        principalTable: "outlets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_tables_table_id",
                        column: x => x.table_id,
                        principalSchema: "pos",
                        principalTable: "tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_lines",
                schema: "pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_lines_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "pos",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ticket_lines_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "pos",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outlets_hotel_unit_code_code",
                schema: "pos",
                table: "outlets",
                columns: new[] { "hotel_unit_code", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_outlet_id_code",
                schema: "pos",
                table: "products",
                columns: new[] { "outlet_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_outlet_id_number",
                schema: "pos",
                table: "tables",
                columns: new[] { "outlet_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_lines_product_id",
                schema: "pos",
                table: "ticket_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_lines_ticket_id",
                schema: "pos",
                table: "ticket_lines",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_number",
                schema: "pos",
                table: "tickets",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_outlet_id",
                schema: "pos",
                table: "tickets",
                column: "outlet_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_table_id",
                schema: "pos",
                table: "tickets",
                column: "table_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_lines",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "products",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "tables",
                schema: "pos");

            migrationBuilder.DropTable(
                name: "outlets",
                schema: "pos");
        }
    }
}
