using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the HR module: the reference data, an employee file with its
/// contract, the time and absence facts, one complete payroll month, and the closing lock.
///
/// Each test provisions its own role carrying exactly the HR permission keys it needs, so the
/// per-permission policies registered in Program.cs are enforced for real - in particular the
/// separation between preparing a payroll (hr.payroll) and closing it (hr.payroll.close).
/// </summary>
public sealed class HumanResourcesEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string HrRead = "hr.read";
    private const string HrWrite = "hr.write";
    private const string HrPayroll = "hr.payroll";
    private const string HrPayrollClose = "hr.payroll.close";

    private const string Period = "2026-04";

    private readonly RaqmiApiFactory _factory;

    public HumanResourcesEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_payroll_month_runs_end_to_end_and_the_closing_locks_it_for_good()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("HRPAY", "HR Payroll Hotel");

        await CreateHrUserAsync("hr.payroll.writer", "hr.payroll.writer@example.com", "RH", HrRead, HrWrite, HrPayroll);
        await CreateHrUserAsync("hr.payroll.closer", "hr.payroll.closer@example.com", "RH cloture", HrRead, HrPayrollClose);

        using var hr = await _factory.CreateAuthenticatedClientAsync("hr.payroll.writer", Password);

        // --- Reference data -------------------------------------------------------------
        var departmentResponse = await hr.PostAsJsonAsync(
            "/api/v1/hr/departments",
            new CreateDepartmentRequest("RECEPTION", "Reception"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, departmentResponse.StatusCode);

        var positionResponse = await hr.PostAsJsonAsync(
            "/api/v1/hr/positions",
            new CreatePositionRequest("RECEP", "Receptionniste", "RECEPTION", 20_000m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, positionResponse.StatusCode);

        // --- Statutory parameters -------------------------------------------------------
        var parametersResponse = await hr.PostAsJsonAsync(
            "/api/v1/hr/payroll/parameters",
            StatutoryParameters("2026-01"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, parametersResponse.StatusCode);

        // --- Employee and contract ------------------------------------------------------
        var employee = await CreateEmployeeAsync(hr, "EMP-100", "Amina", "Belkacem", unitCode);

        // The list projection must never expose the legal identifiers - only the detail call does.
        var summaries = await hr.GetFromJsonAsync<EmployeeSummaryResponse[]>(
            $"/api/v1/hr/employees?hotelUnitCode={unitCode}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(summaries);
        var summary = Assert.Single(summaries!, row => row.EmployeeNumber == "EMP-100");
        Assert.Equal(EmployeeStatus.Active, summary.Status);

        var contractResponse = await hr.PostAsJsonAsync(
            $"/api/v1/hr/employees/{employee.Id}/contracts",
            new CreateContractRequest(
                ContractType.Permanent,
                new DateOnly(2026, 1, 1),
                null,
                60_000m,
                40m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, contractResponse.StatusCode);

        // A second active contract for the same employee is refused.
        var duplicateContract = await hr.PostAsJsonAsync(
            $"/api/v1/hr/employees/{employee.Id}/contracts",
            new CreateContractRequest(ContractType.Permanent, new DateOnly(2026, 2, 1), null, 70_000m, 40m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, duplicateContract.StatusCode);

        // An employee with no contract at all: the run must report it and still pay the others.
        var withoutContract = await CreateEmployeeAsync(hr, "EMP-101", "Omar", "Haddad", unitCode);
        Assert.NotEqual(employee.Id, withoutContract.Id);

        // --- Time, absence and bonus ----------------------------------------------------
        var validatedEntry = await SaveTimeEntryAsync(hr, employee.Id, new DateOnly(2026, 4, 1), 8m);
        await SaveTimeEntryAsync(hr, employee.Id, new DateOnly(2026, 4, 2), 7m);

        var validateEntryResponse = await hr.PostAsync(
            $"/api/v1/hr/time-entries/{validatedEntry.Id}/validate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, validateEntryResponse.StatusCode);

        var absenceResponse = await hr.PostAsJsonAsync(
            "/api/v1/hr/absences",
            new CreateAbsenceRequest(
                employee.Id,
                AbsenceType.UnpaidLeave,
                new DateOnly(2026, 4, 10),
                new DateOnly(2026, 4, 12),
                "Conge sans solde"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, absenceResponse.StatusCode);

        var absence = await absenceResponse.Content.ReadFromJsonAsync<AbsenceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(absence);
        Assert.True(absence!.IsUnpaid);

        var approveResponse = await hr.PostAsJsonAsync(
            $"/api/v1/hr/absences/{absence.Id}/approve",
            new DecideAbsenceRequest("Accord du chef de service"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var bonusResponse = await hr.PostAsJsonAsync(
            $"/api/v1/hr/payroll/periods/{Period}/bonuses",
            new CreateBonusRequest(employee.Id, "PRIME", "Prime de rendement", 10_000m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, bonusResponse.StatusCode);

        // --- Pre-payroll run ------------------------------------------------------------
        var run = await PostAndReadAsync<PrePayrollRunResponse>(
            hr,
            $"/api/v1/hr/payroll/periods/{Period}/generate");

        Assert.Equal(Period, run.Period);
        Assert.Equal(1, run.Generated);
        Assert.Equal(0, run.Updated);
        Assert.Equal(0, run.SkippedValidated);
        Assert.Equal(1, run.EmployeesWithoutContract);
        Assert.Contains(run.Warnings, warning => warning.Contains("EMP-101"));

        var payslips = await hr.GetFromJsonAsync<PayslipResponse[]>(
            $"/api/v1/hr/payroll/periods/{Period}/payslips",
            RaqmiApiFactory.JsonOptions);

        var payslip = Assert.Single(payslips!);

        // Every figure below is hand-checkable from the rules:
        //   base 60 000, only the VALIDATED 8 hours count (the 7 draft hours do not),
        //   3 unpaid days at 60 000 / 30 = 2 000 deduct 6 000,
        //   bonus 10 000, so gross = 60 000 + 10 000 - 6 000 = 64 000;
        //   CNAS 9% = 5 760; IRG base = 64 000 - 5 760 - 40 000 = 18 240, taxed at 23% = 4 195.20;
        //   net = 64 000 - 5 760 - 4 195.20 = 54 044.80.
        Assert.Equal(60_000m, payslip.BaseGross);
        Assert.Equal(8m, payslip.HoursWorked);
        Assert.Equal(0m, payslip.OvertimeHours);
        Assert.Equal(3m, payslip.UnpaidAbsenceDays);
        Assert.Equal(6_000m, payslip.AbsenceDeduction);
        Assert.Equal(10_000m, payslip.BonusTotal);
        Assert.Equal(64_000m, payslip.TaxableGross);
        Assert.Equal(5_760m, payslip.EmployeeSocialContribution);
        Assert.Equal(18_240m, payslip.IncomeTaxBase);
        Assert.Equal(4_195.20m, payslip.IncomeTax);
        Assert.Equal(54_044.80m, payslip.NetPay);
        Assert.Equal(16_640m, payslip.EmployerSocialContribution);
        Assert.Equal(2_400m, payslip.EmployerPayrollTaxes);
        Assert.Equal(83_040m, payslip.EmployerCost);
        Assert.False(payslip.BelowMinimumWage);
        Assert.Equal(PayslipStatus.Draft, payslip.Status);

        // --- Validation and the idempotence of the run ----------------------------------
        var validatePayslip = await hr.PostAsync(
            $"/api/v1/hr/payroll/periods/{Period}/payslips/{payslip.Id}/validate",
            content: null);

        Assert.Equal(HttpStatusCode.OK, validatePayslip.StatusCode);

        var secondRun = await PostAndReadAsync<PrePayrollRunResponse>(
            hr,
            $"/api/v1/hr/payroll/periods/{Period}/generate");

        // The signed-off payslip was left alone, and the operator is told so.
        Assert.Equal(0, secondRun.Generated);
        Assert.Equal(0, secondRun.Updated);
        Assert.Equal(1, secondRun.SkippedValidated);

        // --- Closing needs its own permission -------------------------------------------
        var refusedClose = await hr.PostAsync($"/api/v1/hr/payroll/periods/{Period}/validate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, refusedClose.StatusCode);

        using var closer = await _factory.CreateAuthenticatedClientAsync("hr.payroll.closer", Password);

        var validatePeriod = await closer.PostAsync($"/api/v1/hr/payroll/periods/{Period}/validate", content: null);
        Assert.Equal(HttpStatusCode.OK, validatePeriod.StatusCode);

        var closePeriod = await closer.PostAsync($"/api/v1/hr/payroll/periods/{Period}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closePeriod.StatusCode);

        var closed = await closePeriod.Content.ReadFromJsonAsync<PayrollPeriodResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(closed);
        Assert.Equal(PayrollPeriodStatus.Closed, closed!.Status);
        Assert.Equal(1, closed.PayslipCount);
        Assert.Equal(0, closed.DraftPayslipCount);
        Assert.Equal(64_000m, closed.TotalTaxableGross);
        Assert.Equal(54_044.80m, closed.TotalNetPay);

        // --- The lock -------------------------------------------------------------------
        var lockedRun = await hr.PostAsync($"/api/v1/hr/payroll/periods/{Period}/generate", content: null);
        Assert.Equal(HttpStatusCode.Conflict, lockedRun.StatusCode);

        var lockedBonus = await hr.PostAsJsonAsync(
            $"/api/v1/hr/payroll/periods/{Period}/bonuses",
            new CreateBonusRequest(employee.Id, "PRIME2", "Prime tardive", 5_000m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, lockedBonus.StatusCode);

        var lockedTimeEntry = await hr.PostAsJsonAsync(
            "/api/v1/hr/time-entries",
            new SaveTimeEntryRequest(employee.Id, new DateOnly(2026, 4, 20), 8m, TimeEntrySource.Manual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, lockedTimeEntry.StatusCode);

        var lockedUnpaidAbsence = await hr.PostAsJsonAsync(
            "/api/v1/hr/absences",
            new CreateAbsenceRequest(
                withoutContract.Id,
                AbsenceType.UnpaidLeave,
                new DateOnly(2026, 4, 5),
                new DateOnly(2026, 4, 6),
                null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, lockedUnpaidAbsence.StatusCode);

        // A PAID absence is still recordable against a closed month: it changes no payslip figure,
        // and the HR file has to stay truthful even once the payroll is frozen.
        var paidAbsenceOnClosedMonth = await hr.PostAsJsonAsync(
            "/api/v1/hr/absences",
            new CreateAbsenceRequest(
                withoutContract.Id,
                AbsenceType.SickLeave,
                new DateOnly(2026, 4, 5),
                new DateOnly(2026, 4, 6),
                "Arret maladie"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, paidAbsenceOnClosedMonth.StatusCode);

        // A later month is untouched by the lock.
        var nextMonth = await hr.PostAsync("/api/v1/hr/payroll/periods/2026-05/generate", content: null);
        Assert.Equal(HttpStatusCode.OK, nextMonth.StatusCode);
    }

    [Fact]
    public async Task Reading_the_module_never_grants_the_right_to_change_it()
    {
        await _factory.CreateHotelUnitAsync("HRRO", "HR Read Only Hotel");
        await CreateHrUserAsync("hr.reader", "hr.reader@example.com", "Lecteur RH", HrRead);

        using var reader = await _factory.CreateAuthenticatedClientAsync("hr.reader", Password);

        var listing = await reader.GetAsync("/api/v1/hr/employees");
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);

        var refusedDepartment = await reader.PostAsJsonAsync(
            "/api/v1/hr/departments",
            new CreateDepartmentRequest("READONLY", "Lecture seule"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, refusedDepartment.StatusCode);

        var refusedRun = await reader.PostAsync("/api/v1/hr/payroll/periods/2026-07/generate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, refusedRun.StatusCode);

        var refusedClose = await reader.PostAsync("/api/v1/hr/payroll/periods/2026-07/close", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, refusedClose.StatusCode);
    }

    [Fact]
    public async Task A_run_without_registered_statutory_parameters_is_refused_rather_than_guessed()
    {
        await CreateHrUserAsync(
            "hr.noparams",
            "hr.noparams@example.com",
            "RH sans bareme",
            HrRead, HrWrite, HrPayroll);

        using var hr = await _factory.CreateAuthenticatedClientAsync("hr.noparams", Password);

        // 2000-01 predates any parameter set this class registers, so no version governs it.
        var response = await hr.PostAsync("/api/v1/hr/payroll/periods/2000-01/generate", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unparsable_period_is_a_validation_error()
    {
        await CreateHrUserAsync("hr.badperiod", "hr.badperiod@example.com", "RH", HrRead, HrPayroll);

        using var hr = await _factory.CreateAuthenticatedClientAsync("hr.badperiod", Password);

        var response = await hr.GetAsync("/api/v1/hr/payroll/periods/2026-13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CreatePayrollParameterSetRequest StatutoryParameters(string effectiveFrom)
    {
        return new CreatePayrollParameterSetRequest(
            effectiveFrom,
            "Parametres portes depuis Hotel Metrics Pro",
            MonthlyReferenceHours: 173.33m,
            OvertimeMultiplier: 1.5m,
            ReferenceDaysPerMonth: 30,
            EmployeeSocialRate: 0.09m,
            EmployerSocialRate: 0.26m,
            WorkAccidentRate: 0.0125m,
            UnemploymentInsuranceRate: 0.015m,
            VocationalTrainingRate: 0.01m,
            IncomeTaxAbatement: 40_000m,
            IncomeTaxAbatementPerChild: 1_000m,
            MinimumWage: 20_000m,
            Brackets: new[]
            {
                new IncomeTaxBracketRequest(30_000m, 0.23m),
                new IncomeTaxBracketRequest(120_000m, 0.27m),
                new IncomeTaxBracketRequest(null, 0.33m)
            });
    }

    private static async Task<EmployeeResponse> CreateEmployeeAsync(
        HttpClient client,
        string employeeNumber,
        string firstName,
        string lastName,
        string unitCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/hr/employees",
            new CreateEmployeeRequest(
                employeeNumber,
                firstName,
                lastName,
                unitCode,
                "RECEP",
                new DateOnly(2026, 1, 1),
                Email: null,
                Phone: null,
                NationalIdentityNumber: null,
                SocialSecurityNumber: null,
                BankAccountNumber: null,
                BadgeId: null,
                DependentChildren: 0),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var employee = await response.Content.ReadFromJsonAsync<EmployeeResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(employee);

        return employee!;
    }

    private static async Task<TimeEntryResponse> SaveTimeEntryAsync(
        HttpClient client,
        Guid employeeId,
        DateOnly workDate,
        decimal hours)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/hr/time-entries",
            new SaveTimeEntryRequest(employeeId, workDate, hours, TimeEntrySource.Manual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entry = await response.Content.ReadFromJsonAsync<TimeEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(entry);

        return entry!;
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var value = await response.Content.ReadFromJsonAsync<T>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(value);

        return value!;
    }

    private async Task CreateHrUserAsync(
        string userName,
        string email,
        string displayName,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "HR permission keys are missing from the seeded PermissionCatalog: "
            + string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.hr.{Guid.NewGuid():N}",
            "HR test role",
            "Role dedicated to human resources endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
