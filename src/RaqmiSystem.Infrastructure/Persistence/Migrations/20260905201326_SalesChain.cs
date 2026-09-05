using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_kind",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.AddColumn<Guid>(
                name: "cash_receipt_id",
                schema: "finance",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "article_code",
                schema: "finance",
                table: "invoice_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                schema: "finance",
                table: "invoice_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
            // Reprise : la TVA de ligne est desormais stockee (figeable au centime) ; l'existant
            // est recalcule une fois depuis le HT et le taux, comme le faisait le domaine a la volee.
            migrationBuilder.Sql("UPDATE finance.invoice_lines SET vat_amount = round(line_total_excl_vat * vat_rate / 100, 2);");

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    designation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    family = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    unit_price_excl_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tracks_stock = table.Column<bool>(type: "boolean", nullable: false),
                    stock_item_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles", x => x.id);
                    table.CheckConstraint("ck_articles_stock_link", "(tracks_stock AND stock_item_code IS NOT NULL) OR (NOT tracks_stock AND stock_item_code IS NULL)");
                    table.CheckConstraint("ck_articles_unit_price_non_negative", "CAST(unit_price_excl_vat AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_articles_stock_items_stock_item_code",
                        column: x => x.stock_item_code,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_kind",
                schema: "inventory",
                table: "stock_movements",
                sql: "kind IN ('PurchaseEntry', 'Consumption', 'TransferOut', 'TransferIn', 'InventoryAdjustment', 'Sale')");

            migrationBuilder.CreateIndex(
                name: "ux_invoices_cash_receipt_id",
                schema: "finance",
                table: "invoices",
                column: "cash_receipt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_article_code",
                schema: "finance",
                table: "invoice_lines",
                column: "article_code");

            migrationBuilder.CreateIndex(
                name: "ix_articles_family",
                schema: "catalog",
                table: "articles",
                column: "family");

            migrationBuilder.CreateIndex(
                name: "ix_articles_is_active",
                schema: "catalog",
                table: "articles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_articles_stock_item_code",
                schema: "catalog",
                table: "articles",
                column: "stock_item_code");

            migrationBuilder.CreateIndex(
                name: "ux_articles_code",
                schema: "catalog",
                table: "articles",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_cash_receipts_cash_receipt_id",
                schema: "finance",
                table: "invoices",
                column: "cash_receipt_id",
                principalSchema: "finance",
                principalTable: "cash_receipts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_cash_receipts_cash_receipt_id",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "catalog");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_kind",
                schema: "inventory",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ux_invoices_cash_receipt_id",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "ix_invoice_lines_article_code",
                schema: "finance",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "cash_receipt_id",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "article_code",
                schema: "finance",
                table: "invoice_lines");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                schema: "finance",
                table: "invoice_lines");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_kind",
                schema: "inventory",
                table: "stock_movements",
                sql: "kind IN ('PurchaseEntry', 'Consumption', 'TransferOut', 'TransferIn', 'InventoryAdjustment')");
        }
    }
}
