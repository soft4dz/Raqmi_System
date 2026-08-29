using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1WaveOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    account_number = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.id);
                    table.UniqueConstraint("AK_bank_accounts_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    rc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ai = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    nis = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.UniqueConstraint("AK_customers_code", x => x.code);
                    table.CheckConstraint("ck_customers_customer_type", "customer_type IN ('Company', 'Individual', 'PublicEntity')");
                    table.CheckConstraint("ck_customers_nif_length", "nif IS NULL OR length(nif) = 15");
                });

            migrationBuilder.CreateTable(
                name: "daily_closings",
                schema: "exploitation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    reopened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reopened_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reopen_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_closings", x => x.id);
                    table.CheckConstraint("ck_daily_closings_status", "status IN ('Closed', 'Reopened')");
                    table.ForeignKey(
                        name: "FK_daily_closings_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_receipts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    bank_account_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_cash_receipts", x => x.id);
                    table.CheckConstraint("ck_cash_receipts_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_cash_receipts_method", "method IN ('Cash', 'Card', 'Cheque', 'BankTransfer')");
                    table.CheckConstraint("ck_cash_receipts_status", "status IN ('Draft', 'Confirmed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_cash_receipts_bank_accounts_bank_account_code",
                        column: x => x.bank_account_code,
                        principalSchema: "finance",
                        principalTable: "bank_accounts",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_receipts_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_orders",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_date = table.Column<DateOnly>(type: "date", nullable: false),
                    beneficiary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    bank_account_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_payment_orders", x => x.id);
                    table.CheckConstraint("ck_payment_orders_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_payment_orders_status", "status IN ('Draft', 'Approved', 'Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_payment_orders_bank_accounts_bank_account_code",
                        column: x => x.bank_account_code,
                        principalSchema: "finance",
                        principalTable: "bank_accounts",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    issued_year = table.Column<int>(type: "integer", nullable: true),
                    issued_sequence = table.Column<int>(type: "integer", nullable: true),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_excl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_incl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    issued_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.CheckConstraint("ck_invoices_status", "status IN ('Draft', 'Issued', 'Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_invoices_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    designation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    line_total_excl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.id);
                    table.CheckConstraint("ck_invoice_lines_line_number_positive", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "finance",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_accounts_is_active",
                schema: "finance",
                table: "bank_accounts",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_cash_receipts_bank_account_code",
                schema: "finance",
                table: "cash_receipts",
                column: "bank_account_code");

            migrationBuilder.CreateIndex(
                name: "IX_cash_receipts_hotel_unit_code",
                schema: "finance",
                table: "cash_receipts",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_cash_receipts_method",
                schema: "finance",
                table: "cash_receipts",
                column: "method");

            migrationBuilder.CreateIndex(
                name: "ix_cash_receipts_receipt_date_hotel_unit_code",
                schema: "finance",
                table: "cash_receipts",
                columns: new[] { "receipt_date", "hotel_unit_code" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_receipts_status",
                schema: "finance",
                table: "cash_receipts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_customers_is_active",
                schema: "finance",
                table: "customers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_customers_name",
                schema: "finance",
                table: "customers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_daily_closings_business_date_hotel_unit_code",
                schema: "exploitation",
                table: "daily_closings",
                columns: new[] { "business_date", "hotel_unit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_closings_hotel_unit_code",
                schema: "exploitation",
                table: "daily_closings",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_daily_closings_status",
                schema: "exploitation",
                table: "daily_closings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_invoice_id",
                schema: "finance",
                table: "invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_customer_code",
                schema: "finance",
                table: "invoices",
                column: "customer_code");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_hotel_unit_code",
                schema: "finance",
                table: "invoices",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_invoice_date",
                schema: "finance",
                table: "invoices",
                column: "invoice_date");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_status",
                schema: "finance",
                table: "invoices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_invoices_issued_year_sequence",
                schema: "finance",
                table: "invoices",
                columns: new[] { "issued_year", "issued_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_invoices_number",
                schema: "finance",
                table: "invoices",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_bank_account_code",
                schema: "finance",
                table: "payment_orders",
                column: "bank_account_code");

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_due_date",
                schema: "finance",
                table: "payment_orders",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_payment_orders_status",
                schema: "finance",
                table: "payment_orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_receipts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "daily_closings",
                schema: "exploitation");

            migrationBuilder.DropTable(
                name: "invoice_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payment_orders",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "bank_accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "finance");
        }
    }
}
