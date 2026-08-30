using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "budgeting");

            migrationBuilder.EnsureSchema(
                name: "accounting");

            migrationBuilder.CreateTable(
                name: "budget_plans",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_plans", x => x.id);
                    table.CheckConstraint("ck_budget_plans_status", "status IN ('Draft', 'Approved', 'Closed')");
                    table.CheckConstraint("ck_budget_plans_year", "year BETWEEN 2000 AND 2999");
                    table.ForeignKey(
                        name: "FK_budget_plans_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chart_accounts",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_class = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chart_accounts", x => x.id);
                    table.UniqueConstraint("AK_chart_accounts_code", x => x.code);
                    table.CheckConstraint("ck_chart_accounts_account_class", "account_class BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_chart_accounts_kind", "kind IN ('Asset', 'Liability', 'Equity', 'Revenue', 'Expense')");
                });

            migrationBuilder.CreateTable(
                name: "journals",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journals", x => x.id);
                    table.UniqueConstraint("AK_journals_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "reminders",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sent_at = table.Column<DateOnly>(type: "date", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminders", x => x.id);
                    table.CheckConstraint("ck_reminders_channel", "channel IN ('Phone', 'Email', 'Letter', 'InPerson')");
                    table.CheckConstraint("ck_reminders_level", "level IN ('First', 'Second', 'FormalNotice')");
                    table.ForeignKey(
                        name: "FK_reminders_customers_customer_code",
                        column: x => x.customer_code,
                        principalSchema: "finance",
                        principalTable: "customers",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "budget_lines",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount_target = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_lines", x => x.id);
                    table.CheckConstraint("ck_budget_lines_amount_target_non_negative", "CAST(amount_target AS numeric) >= 0");
                    table.CheckConstraint("ck_budget_lines_category", "category IN ('Accommodation', 'Food', 'Beverage', 'Other')");
                    table.CheckConstraint("ck_budget_lines_month", "month BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_budget_lines_budget_plans_budget_plan_id",
                        column: x => x.budget_plan_id,
                        principalSchema: "budgeting",
                        principalTable: "budget_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_date = table.Column<DateOnly>(type: "date", nullable: false),
                    journal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    posted_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
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
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                    table.CheckConstraint("ck_journal_entries_reverses_not_self", "reverses_entry_id IS NULL OR reverses_entry_id <> id");
                    table.CheckConstraint("ck_journal_entries_status", "status IN ('Draft', 'Posted', 'Cancelled')");
                    table.CheckConstraint("ck_journal_entries_totals_positive", "CAST(total_debit AS numeric) >= 0 AND CAST(total_credit AS numeric) >= 0");
                    table.ForeignKey(
                        name: "FK_journal_entries_journals_journal_code",
                        column: x => x.journal_code,
                        principalSchema: "accounting",
                        principalTable: "journals",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                schema: "accounting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    account_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.CheckConstraint("ck_journal_entry_lines_debit_credit_exclusive", "CAST(debit AS numeric) >= 0 AND CAST(credit AS numeric) >= 0 AND (CAST(debit AS numeric) = 0) <> (CAST(credit AS numeric) = 0)");
                    table.CheckConstraint("ck_journal_entry_lines_line_number_positive", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_chart_accounts_account_code",
                        column: x => x.account_code,
                        principalSchema: "accounting",
                        principalTable: "chart_accounts",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalSchema: "accounting",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_budget_lines_plan_month_category",
                schema: "budgeting",
                table: "budget_lines",
                columns: new[] { "budget_plan_id", "month", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_plans_hotel_unit_code",
                schema: "budgeting",
                table: "budget_plans",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_budget_plans_status",
                schema: "budgeting",
                table: "budget_plans",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_budget_plans_year_hotel_unit",
                schema: "budgeting",
                table: "budget_plans",
                columns: new[] { "year", "hotel_unit_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chart_accounts_account_class",
                schema: "accounting",
                table: "chart_accounts",
                column: "account_class");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_entry_date",
                schema: "accounting",
                table: "journal_entries",
                column: "entry_date");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_journal_code",
                schema: "accounting",
                table: "journal_entries",
                column: "journal_code");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_status",
                schema: "accounting",
                table: "journal_entries",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_journal_entries_reverses_entry_id",
                schema: "accounting",
                table: "journal_entries",
                column: "reverses_entry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_account_code",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "account_code");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_journal_entry_id",
                schema: "accounting",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_journals_is_active",
                schema: "accounting",
                table: "journals",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_customer_code",
                schema: "finance",
                table: "reminders",
                column: "customer_code");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_sent_at",
                schema: "finance",
                table: "reminders",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ux_reminders_invoice_number_level",
                schema: "finance",
                table: "reminders",
                columns: new[] { "invoice_number", "level" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_lines",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "journal_entry_lines",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "reminders",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "budget_plans",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "chart_accounts",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "journals",
                schema: "accounting");
        }
    }
}
