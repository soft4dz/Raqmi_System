using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountingScfCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_number",
                schema: "accounting",
                table: "journal_entries",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fiscal_year_id",
                schema: "accounting",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fiscal_years",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_years", x => x.id);
                    table.CheckConstraint("ck_fiscal_year_dates", "ends_on >= starts_on");
                    table.CheckConstraint("ck_fiscal_year_status", "status IN ('Open','Closed')");
                });

            migrationBuilder.CreateTable(
                name: "parties",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_sequences",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_number = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_sequences", x => x.id);
                    table.CheckConstraint("ck_journal_sequence_non_negative", "last_number >= 0");
                    table.ForeignKey(
                        name: "FK_journal_sequences_fiscal_years_fiscal_year_id",
                        column: x => x.fiscal_year_id,
                        principalSchema: "accounting",
                        principalTable: "fiscal_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_sequences_journals_journal_code",
                        column: x => x.journal_code,
                        principalSchema: "accounting",
                        principalTable: "journals",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "periods",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periods", x => x.id);
                    table.CheckConstraint("ck_period_dates", "ends_on >= starts_on");
                    table.CheckConstraint("ck_period_number", "number BETWEEN 1 AND 16");
                    table.ForeignKey(
                        name: "FK_periods_fiscal_years_fiscal_year_id",
                        column: x => x.fiscal_year_id,
                        principalSchema: "accounting",
                        principalTable: "fiscal_years",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliations",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matched_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliations", x => x.id);
                    table.CheckConstraint("ck_reconciliation_amount", "matched_amount > 0");
                    table.ForeignKey(
                        name: "FK_reconciliations_parties_party_id",
                        column: x => x.party_id,
                        principalSchema: "accounting",
                        principalTable: "parties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_allocations",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_allocations", x => x.id);
                    table.CheckConstraint("ck_reconciliation_allocation_amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_reconciliation_allocations_journal_entry_lines_journal_entr~",
                        column: x => x.journal_entry_line_id,
                        principalSchema: "accounting",
                        principalTable: "journal_entry_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_allocations_reconciliations_reconciliation_id",
                        column: x => x.reconciliation_id,
                        principalSchema: "accounting",
                        principalTable: "reconciliations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_fiscal_year_id",
                schema: "accounting",
                table: "journal_entries",
                column: "fiscal_year_id");

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_document_number",
                schema: "accounting",
                table: "journal_entries",
                column: "document_number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_journal_entries_posted_balanced",
                schema: "accounting",
                table: "journal_entries",
                sql: "status <> 'Posted' OR CAST(total_debit AS numeric) = CAST(total_credit AS numeric)");

            migrationBuilder.CreateIndex(
                name: "ux_fiscal_years_code",
                schema: "accounting",
                table: "fiscal_years",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_sequences_fiscal_year_id",
                schema: "accounting",
                table: "journal_sequences",
                column: "fiscal_year_id");

            migrationBuilder.CreateIndex(
                name: "IX_journal_sequences_journal_code_fiscal_year_id",
                schema: "accounting",
                table: "journal_sequences",
                columns: new[] { "journal_code", "fiscal_year_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parties_code",
                schema: "accounting",
                table: "parties",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_periods_fiscal_year_id_number",
                schema: "accounting",
                table: "periods",
                columns: new[] { "fiscal_year_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_allocations_journal_entry_line_id",
                schema: "accounting",
                table: "reconciliation_allocations",
                column: "journal_entry_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_allocations_reconciliation_id",
                schema: "accounting",
                table: "reconciliation_allocations",
                column: "reconciliation_id");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliations_code",
                schema: "accounting",
                table: "reconciliations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliations_party_id",
                schema: "accounting",
                table: "reconciliations",
                column: "party_id");

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_fiscal_years_fiscal_year_id",
                schema: "accounting",
                table: "journal_entries",
                column: "fiscal_year_id",
                principalSchema: "accounting",
                principalTable: "fiscal_years",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_fiscal_years_fiscal_year_id",
                schema: "accounting",
                table: "journal_entries");

            migrationBuilder.DropTable(
                name: "journal_sequences",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "periods",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "reconciliation_allocations",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "fiscal_years",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "reconciliations",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "accounting");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_fiscal_year_id",
                schema: "accounting",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ux_journal_entries_document_number",
                schema: "accounting",
                table: "journal_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_journal_entries_posted_balanced",
                schema: "accounting",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "document_number",
                schema: "accounting",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "fiscal_year_id",
                schema: "accounting",
                table: "journal_entries");
        }
    }
}
