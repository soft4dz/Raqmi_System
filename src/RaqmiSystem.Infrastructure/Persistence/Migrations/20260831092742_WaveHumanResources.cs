using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaqmiSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaveHumanResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                    table.UniqueConstraint("AK_departments_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "payroll_parameter_sets",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    monthly_reference_hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    overtime_multiplier = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    reference_days_per_month = table.Column<int>(type: "integer", nullable: false),
                    employee_social_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    employer_social_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    work_accident_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    unemployment_insurance_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    vocational_training_rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    income_tax_abatement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax_abatement_per_child = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    minimum_wage = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_parameter_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_periods",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payslip_count = table.Column<int>(type: "integer", nullable: false),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    department_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    minimum_gross_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.id);
                    table.UniqueConstraint("AK_positions_code", x => x.code);
                    table.ForeignKey(
                        name: "FK_positions_departments_department_code",
                        column: x => x.department_code,
                        principalSchema: "hr",
                        principalTable: "departments",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_tax_brackets",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    upper_bound = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    parameter_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_tax_brackets", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_tax_brackets_payroll_parameter_sets_parameter_set_id",
                        column: x => x.parameter_set_id,
                        principalSchema: "hr",
                        principalTable: "payroll_parameter_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    first_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    last_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    hotel_unit_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    position_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    national_identity_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    social_security_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    bank_account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    badge_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    dependent_children = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employees_hotel_units_hotel_unit_code",
                        column: x => x.hotel_unit_code,
                        principalSchema: "organization",
                        principalTable: "hotel_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employees_positions_position_code",
                        column: x => x.position_code,
                        principalSchema: "hr",
                        principalTable: "positions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "absences",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    decision_note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_absences", x => x.id);
                    table.ForeignKey(
                        name: "FK_absences_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_contracts",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    gross_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    weekly_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    terminated_on = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_employment_contracts_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_bonuses",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_bonuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_bonuses_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payslips",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    base_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    hours_worked = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    overtime_hours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    overtime_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unpaid_absence_days = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    absence_deduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bonus_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    taxable_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employee_social_contribution = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    income_tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net_pay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_social_contribution = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_work_accident = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_unemployment_insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_vocational_training = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_payroll_taxes = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    below_minimum_wage = table.Column<bool>(type: "boolean", nullable: false),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payslips", x => x.id);
                    table.ForeignKey(
                        name: "FK_payslips_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "time_entries",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours_worked = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    validated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_time_entries_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hr_absences_employee_range",
                schema: "hr",
                table: "absences",
                columns: new[] { "employee_id", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_absences_status",
                schema: "hr",
                table: "absences",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_hr_departments_code",
                schema: "hr",
                table: "departments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_employees_hotel_unit_code",
                schema: "hr",
                table: "employees",
                column: "hotel_unit_code");

            migrationBuilder.CreateIndex(
                name: "ix_hr_employees_position_code",
                schema: "hr",
                table: "employees",
                column: "position_code");

            migrationBuilder.CreateIndex(
                name: "ux_hr_employees_badge_id",
                schema: "hr",
                table: "employees",
                column: "badge_id",
                unique: true,
                filter: "badge_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_hr_employees_employee_number",
                schema: "hr",
                table: "employees",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_contracts_employee_id",
                schema: "hr",
                table: "employment_contracts",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_hr_contracts_active_per_employee",
                schema: "hr",
                table: "employment_contracts",
                column: "employee_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_hr_payroll_bonuses_period_employee",
                schema: "hr",
                table: "payroll_bonuses",
                columns: new[] { "period", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_bonuses_employee_id",
                schema: "hr",
                table: "payroll_bonuses",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_hr_payroll_parameter_sets_effective_from",
                schema: "hr",
                table: "payroll_parameter_sets",
                column: "effective_from",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_hr_payroll_periods_period",
                schema: "hr",
                table: "payroll_periods",
                column: "period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_hr_payroll_tax_brackets_set_ordinal",
                schema: "hr",
                table: "payroll_tax_brackets",
                columns: new[] { "parameter_set_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payslips_employee_id",
                schema: "hr",
                table: "payslips",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_hr_payslips_period_employee",
                schema: "hr",
                table: "payslips",
                columns: new[] { "period", "employee_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_positions_department_code",
                schema: "hr",
                table: "positions",
                column: "department_code");

            migrationBuilder.CreateIndex(
                name: "ux_hr_positions_code",
                schema: "hr",
                table: "positions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_hr_time_entries_employee_date",
                schema: "hr",
                table: "time_entries",
                columns: new[] { "employee_id", "work_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absences",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employment_contracts",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_bonuses",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_periods",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_tax_brackets",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payslips",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "time_entries",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_parameter_sets",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "hr");
        }
    }
}
