using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FiscaliteModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fiscalite");

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                schema: "purchasing",
                table: "purchase_order_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                schema: "purchasing",
                table: "purchase_order_lines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Reprise : les commandes existantes n'avaient aucune TVA de ligne ; on applique le
            // taux normal (19%) comme valeur de depart, exactement comme la migration SalesChain
            // l'a fait pour finance.invoice_lines.
            migrationBuilder.Sql(
                "UPDATE purchasing.purchase_order_lines SET vat_rate = 19, " +
                "vat_amount = round(line_total_excl_vat * 19 / 100, 2);");

            migrationBuilder.CreateTable(
                name: "fiscal_returns",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_returns", x => x.id);
                    table.CheckConstraint("ck_fiscal_return_kind", "kind IN ('Simple','Avancee')");
                });

            migrationBuilder.CreateTable(
                name: "sifec_config",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    singleton_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    api_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    api_key_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    declarant_nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_connection_test_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_connection_test_succeeded = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sifec_config", x => x.id);
                    table.CheckConstraint("ck_sifec_config_declarant_nif_length", "declarant_nif IS NULL OR length(declarant_nif) = 15");
                    table.CheckConstraint("ck_sifec_config_mode", "mode IN ('Sandbox','Production')");
                    table.CheckConstraint("ck_sifec_config_singleton", "singleton_key = 'GLOBAL'");
                });

            migrationBuilder.CreateTable(
                name: "sifec_transmissions",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    uid = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    qr_payload = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    response_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sifec_transmissions", x => x.id);
                    table.CheckConstraint("ck_sifec_transmission_mode", "mode IN ('Sandbox','Production')");
                    table.CheckConstraint("ck_sifec_transmission_status", "status IN ('Prepare','Soumis','Accepte','Rejete','Erreur')");
                    table.ForeignKey(
                        name: "FK_sifec_transmissions_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "finance",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vat_declarations",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    base_ht_ventes = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tva_collectee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tva_deductible = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit_anterieur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    solde = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    dgi_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_declarations", x => x.id);
                    table.CheckConstraint("ck_vat_declaration_month", "month BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_vat_declaration_status", "status IN ('Calculee','Exportee','Declaree')");
                });

            migrationBuilder.CreateTable(
                name: "vat_purchase_register_entries",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    piece_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    piece_date = table.Column<DateOnly>(type: "date", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    base_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_purchase_register_entries", x => x.id);
                    table.CheckConstraint("ck_vat_purchase_register_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(vat_amount AS numeric) >= 0");
                    table.CheckConstraint("ck_vat_purchase_register_source", "source IN ('Manuel','Achats')");
                    table.ForeignKey(
                        name: "FK_vat_purchase_register_entries_purchase_orders_purchase_orde~",
                        column: x => x.purchase_order_id,
                        principalSchema: "purchasing",
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vat_sales_register_entries",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    piece_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    piece_date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_nif = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    base_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_sales_register_entries", x => x.id);
                    table.CheckConstraint("ck_vat_sales_register_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(vat_amount AS numeric) >= 0");
                    table.CheckConstraint("ck_vat_sales_register_type", "type IN ('Vente','Avoir')");
                    table.ForeignKey(
                        name: "FK_vat_sales_register_entries_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "finance",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withholding_tax_entries",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    montant_retenu = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_tax_entries", x => x.id);
                    table.CheckConstraint("ck_withholding_tax_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(rate AS numeric) BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "fiscal_return_lines",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_return_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_return_lines_fiscal_returns_fiscal_return_id",
                        column: x => x.fiscal_return_id,
                        principalSchema: "fiscalite",
                        principalTable: "fiscal_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tele_declarations",
                schema: "fiscalite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vat_declaration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    dgi_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    exported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tele_declarations", x => x.id);
                    table.CheckConstraint("ck_tele_declaration_status", "status IN ('Calculee','Exportee','Declaree')");
                    table.CheckConstraint("ck_tele_declaration_type", "type IN ('Tva')");
                    table.ForeignKey(
                        name: "FK_tele_declarations_vat_declarations_vat_declaration_id",
                        column: x => x.vat_declaration_id,
                        principalSchema: "fiscalite",
                        principalTable: "vat_declarations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_return_lines_fiscal_return_id",
                schema: "fiscalite",
                table: "fiscal_return_lines",
                column: "fiscal_return_id");

            migrationBuilder.CreateIndex(
                name: "ux_fiscal_returns_year_kind",
                schema: "fiscalite",
                table: "fiscal_returns",
                columns: new[] { "year", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_sifec_config_singleton_key",
                schema: "fiscalite",
                table: "sifec_config",
                column: "singleton_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sifec_transmissions_invoice_id",
                schema: "fiscalite",
                table: "sifec_transmissions",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_tele_declarations_vat_declaration_id",
                schema: "fiscalite",
                table: "tele_declarations",
                column: "vat_declaration_id");

            migrationBuilder.CreateIndex(
                name: "ux_vat_declarations_period",
                schema: "fiscalite",
                table: "vat_declarations",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vat_purchase_register_piece_date",
                schema: "fiscalite",
                table: "vat_purchase_register_entries",
                column: "piece_date");

            migrationBuilder.CreateIndex(
                name: "ix_vat_purchase_register_purchase_order_id",
                schema: "fiscalite",
                table: "vat_purchase_register_entries",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_vat_sales_register_invoice_id",
                schema: "fiscalite",
                table: "vat_sales_register_entries",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_vat_sales_register_piece_date",
                schema: "fiscalite",
                table: "vat_sales_register_entries",
                column: "piece_date");

            migrationBuilder.CreateIndex(
                name: "ix_withholding_tax_entries_date",
                schema: "fiscalite",
                table: "withholding_tax_entries",
                column: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_return_lines",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "sifec_config",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "sifec_transmissions",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "tele_declarations",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "vat_purchase_register_entries",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "vat_sales_register_entries",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "withholding_tax_entries",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "fiscal_returns",
                schema: "fiscalite");

            migrationBuilder.DropTable(
                name: "vat_declarations",
                schema: "fiscalite");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                schema: "purchasing",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                schema: "purchasing",
                table: "purchase_order_lines");
        }
    }
}
