using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RevenueCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_lines_category",
                schema: "budgeting",
                table: "budget_lines");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                schema: "budgeting",
                table: "budget_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateTable(
                name: "revenue_categories",
                schema: "exploitation",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sector = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_categories", x => x.code);
                    table.CheckConstraint("ck_revenue_categories_display_order_non_negative", "display_order >= 0");
                    table.CheckConstraint("ck_revenue_categories_sector", "sector IS NULL OR sector IN ('Hospitality', 'Trade', 'Services', 'Industry', 'Other')");
                });

            migrationBuilder.CreateTable(
                name: "daily_revenue_lines",
                schema: "exploitation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    daily_revenue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_revenue_lines", x => x.id);
                    table.CheckConstraint("ck_daily_revenue_lines_amount_non_negative", "CAST(amount AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_daily_revenue_lines_daily_revenues_daily_revenue_id",
                        column: x => x.daily_revenue_id,
                        principalSchema: "exploitation",
                        principalTable: "daily_revenues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_revenue_lines_revenue_categories_category_code",
                        column: x => x.category_code,
                        principalSchema: "exploitation",
                        principalTable: "revenue_categories",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "exploitation",
                table: "revenue_categories",
                columns: new[] { "code", "display_order", "is_active", "label", "sector" },
                values: new object[,]
                {
                    { "ACCOMMODATION", 10, true, "Hébergement", "Hospitality" },
                    { "BEVERAGE", 30, true, "Bar", "Hospitality" },
                    { "FOOD", 20, true, "Restauration", "Hospitality" },
                    { "MERCHANDISE", 110, true, "Ventes de marchandises", null },
                    { "OTHER", 40, true, "Autres", "Hospitality" },
                    { "OTHER_INCOME", 130, true, "Autres produits", null },
                    { "SERVICES", 120, true, "Prestations de services", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_budget_lines_category",
                schema: "budgeting",
                table: "budget_lines",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_daily_revenue_lines_category_code",
                schema: "exploitation",
                table: "daily_revenue_lines",
                column: "category_code");

            migrationBuilder.CreateIndex(
                name: "ux_daily_revenue_lines_revenue_category",
                schema: "exploitation",
                table: "daily_revenue_lines",
                columns: new[] { "daily_revenue_id", "category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_revenue_categories_display_order",
                schema: "exploitation",
                table: "revenue_categories",
                column: "display_order");

            // Reprise des données : les quatre montants historiques deviennent des lignes par
            // catégorie (un montant nul ne produit pas de ligne, règle du domaine), et les codes
            // de catégorie budgétaire passent en majuscules avant la pose de la clé étrangère.
            // Placé après les index (ON CONFLICT exige l'index unique) et avant la FK.
            migrationBuilder.Sql("""
                INSERT INTO exploitation.daily_revenue_lines (id, daily_revenue_id, category_code, amount)
                SELECT gen_random_uuid(), r.id, c.code, c.amount
                FROM exploitation.daily_revenues r
                CROSS JOIN LATERAL (VALUES
                    ('ACCOMMODATION', r.accommodation),
                    ('FOOD', r.food),
                    ('BEVERAGE', r.beverage),
                    ('OTHER', r.other_revenue)) AS c(code, amount)
                WHERE c.amount <> 0
                ON CONFLICT (daily_revenue_id, category_code) DO NOTHING;

                UPDATE budgeting.budget_lines
                SET category = UPPER(category)
                WHERE category IN ('Accommodation', 'Food', 'Beverage', 'Other');
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_budget_lines_revenue_categories_category",
                schema: "budgeting",
                table: "budget_lines",
                column: "category",
                principalSchema: "exploitation",
                principalTable: "revenue_categories",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_budget_lines_revenue_categories_category",
                schema: "budgeting",
                table: "budget_lines");

            migrationBuilder.DropTable(
                name: "daily_revenue_lines",
                schema: "exploitation");

            migrationBuilder.DropTable(
                name: "revenue_categories",
                schema: "exploitation");

            migrationBuilder.DropIndex(
                name: "ix_budget_lines_category",
                schema: "budgeting",
                table: "budget_lines");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                schema: "budgeting",
                table: "budget_lines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AddCheckConstraint(
                name: "ck_budget_lines_category",
                schema: "budgeting",
                table: "budget_lines",
                sql: "category IN ('Accommodation', 'Food', 'Beverage', 'Other')");
        }
    }
}
