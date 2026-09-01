using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Human resources module: position reference data, employee files, contracts, time and
/// absences, and the Algerian payroll. Policy names are the permission keys registered in
/// Program.cs from PermissionCatalog.
///
/// Four permissions, drawn along what an act actually engages:
/// <list type="bullet">
///   <item>hr.read - consult the module;</item>
///   <item>hr.write - manage people, contracts, time and absences;</item>
///   <item>hr.payroll - statutory parameters, bonuses, the run and payslip validation;</item>
///   <item>hr.payroll.close - validate then CLOSE a period, which locks the month for good.</item>
/// </list>
/// Closing has its own permission for the same reason closing.reopen does: it is irreversible.
///
/// Depuis le lot 2.1 les routes portent les cles CIBLES du registre (PermissionRegistry) :
/// hr.employee.read, hr.employee.manage (personnes et contrats), hr.time.manage (pointages et
/// absences), hr.payroll.process et hr.payroll.close - cette derniere etait deja au format
/// cible. Chaque politique accepte encore la cle historique qui la couvre : hr.write, composite,
/// vaut les deux cles manage, et aucune des deux ne vaut hr.write.
/// </summary>
internal static class HumanResourcesEndpoints
{
    private const string HrRead = PermissionCatalog.HrEmployeeRead;

    // hr.write est composite dans le registre : gerer les personnes (departements, postes, dossiers,
    // contrats) et gerer le temps (pointages, absences) sont deux gestes, et chaque route porte le
    // sien. Un profil qui ne detient que la cle historique garde l'acces aux deux.
    private const string HrEmployeeWrite = PermissionCatalog.HrEmployeeManage;

    private const string HrTimeWrite = PermissionCatalog.HrTimeManage;

    private const string HrPayroll = PermissionCatalog.HrPayrollProcess;

    private const string HrPayrollClose = PermissionCatalog.HrPayrollClose;

    public static RouteGroupBuilder MapHumanResourcesEndpoints(this RouteGroupBuilder api)
    {
        MapDepartmentEndpoints(api);
        MapPositionEndpoints(api);
        MapEmployeeEndpoints(api);
        MapContractEndpoints(api);
        MapTimeEndpoints(api);
        MapAbsenceEndpoints(api);
        MapPayrollParameterEndpoints(api);
        MapPayrollPeriodEndpoints(api);
        return api;
    }

    private static void MapDepartmentEndpoints(RouteGroupBuilder api)
    {
        var departments = api.MapGroup("/hr/departments").WithTags("HumanResources");

        departments.MapGet("", async (
            bool? includeInactive,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListDepartmentsAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        departments.MapPost("", async (
            CreateDepartmentRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateDepartmentAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/hr/departments/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        departments.MapPut("/{code}", async (
            string code,
            UpdateDepartmentRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateDepartmentAsync(
                code,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        departments.MapPost("/{code}/activate", async (
            string code,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetDepartmentActiveAsync(
                code,
                true,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        departments.MapPost("/{code}/deactivate", async (
            string code,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetDepartmentActiveAsync(
                code,
                false,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);
    }

    private static void MapPositionEndpoints(RouteGroupBuilder api)
    {
        var positions = api.MapGroup("/hr/positions").WithTags("HumanResources");

        positions.MapGet("", async (
            string? departmentCode,
            bool? includeInactive,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPositionsAsync(
                departmentCode,
                includeInactive == true,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        positions.MapPost("", async (
            CreatePositionRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePositionAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/hr/positions/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        positions.MapPut("/{code}", async (
            string code,
            UpdatePositionRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePositionAsync(
                code,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        positions.MapPost("/{code}/activate", async (
            string code,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPositionActiveAsync(
                code,
                true,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        positions.MapPost("/{code}/deactivate", async (
            string code,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetPositionActiveAsync(
                code,
                false,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);
    }

    private static void MapEmployeeEndpoints(RouteGroupBuilder api)
    {
        var employees = api.MapGroup("/hr/employees").WithTags("HumanResources");

        employees.MapGet("", async (
            string? hotelUnitCode,
            EmployeeStatus? status,
            string? search,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListEmployeesAsync(hotelUnitCode, status, search, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        // Reading one file exposes the identifiers protected by law 18-07, so this call is
        // audited by the service - which is why it takes the operation context like a write does.
        employees.MapGet("/{id:guid}", async (
            Guid id,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetEmployeeAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(HrRead);

        employees.MapPost("", async (
            CreateEmployeeRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateEmployeeAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/hr/employees/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        employees.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEmployeeRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateEmployeeAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        employees.MapPost("/{id:guid}/suspend", async (
            Guid id,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetEmployeeSuspendedAsync(
                id,
                true,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        employees.MapPost("/{id:guid}/reactivate", async (
            Guid id,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetEmployeeSuspendedAsync(
                id,
                false,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        employees.MapPost("/{id:guid}/terminate", async (
            Guid id,
            TerminateEmployeeRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.TerminateEmployeeAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);
    }

    private static void MapContractEndpoints(RouteGroupBuilder api)
    {
        var contracts = api.MapGroup("/hr/employees/{employeeId:guid}/contracts").WithTags("HumanResources");

        contracts.MapGet("", async (
            Guid employeeId,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListContractsAsync(employeeId, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(HrRead);

        contracts.MapPost("", async (
            Guid employeeId,
            CreateContractRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateContractAsync(
                employeeId,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created(
                    $"/api/v1/hr/employees/{employeeId}/contracts/{result.Value.Id}",
                    result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        contracts.MapPut("/{contractId:guid}", async (
            Guid employeeId,
            Guid contractId,
            UpdateContractRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateContractAsync(
                employeeId,
                contractId,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);

        contracts.MapPost("/{contractId:guid}/end", async (
            Guid employeeId,
            Guid contractId,
            EndContractRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.EndContractAsync(
                employeeId,
                contractId,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrEmployeeWrite);
    }

    private static void MapTimeEndpoints(RouteGroupBuilder api)
    {
        var timeEntries = api.MapGroup("/hr/time-entries").WithTags("HumanResources");

        timeEntries.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            Guid? employeeId,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            // The range is required rather than defaulted to "this month": a listing whose bounds
            // depend on the server clock returns different rows to two clients asking the same
            // question, and the desktop always knows which period it is showing.
            if (from is null || to is null)
            {
                return Results.BadRequest(new ErrorResponse("The from and to dates are required."));
            }

            if (to < from)
            {
                return Results.BadRequest(new ErrorResponse("The to date cannot precede the from date."));
            }

            var result = await service.ListTimeEntriesAsync(
                employeeId,
                from.Value,
                to.Value,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        timeEntries.MapPost("", async (
            SaveTimeEntryRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SaveTimeEntryAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);

        timeEntries.MapPost("/{id:guid}/validate", async (
            Guid id,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ValidateTimeEntryAsync(
                id,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);
    }

    private static void MapAbsenceEndpoints(RouteGroupBuilder api)
    {
        var absences = api.MapGroup("/hr/absences").WithTags("HumanResources");

        absences.MapGet("", async (
            Guid? employeeId,
            AbsenceStatus? status,
            DateOnly? from,
            DateOnly? to,
            IHumanResourcesService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListAbsencesAsync(employeeId, status, from, to, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        absences.MapPost("", async (
            CreateAbsenceRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAbsenceAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/hr/absences/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);

        absences.MapPost("/{id:guid}/approve", async (
            Guid id,
            DecideAbsenceRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAbsenceAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);

        absences.MapPost("/{id:guid}/reject", async (
            Guid id,
            DecideAbsenceRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAbsenceAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);

        absences.MapPost("/{id:guid}/cancel", async (
            Guid id,
            DecideAbsenceRequest request,
            IHumanResourcesService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAbsenceAsync(
                id,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrTimeWrite);
    }

    private static void MapPayrollParameterEndpoints(RouteGroupBuilder api)
    {
        var parameters = api.MapGroup("/hr/payroll/parameters").WithTags("HumanResources");

        parameters.MapGet("", async (
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListParameterSetsAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        parameters.MapPost("", async (
            CreatePayrollParameterSetRequest request,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateParameterSetAsync(
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/hr/payroll/parameters/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(HrPayroll);
    }

    private static void MapPayrollPeriodEndpoints(RouteGroupBuilder api)
    {
        var periods = api.MapGroup("/hr/payroll/periods").WithTags("HumanResources");

        periods.MapGet("", async (
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPeriodsAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(HrRead);

        periods.MapGet("/{period}", async (
            string period,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetPeriodAsync(period, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(HrRead);

        periods.MapGet("/{period}/bonuses", async (
            string period,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListBonusesAsync(period, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(HrRead);

        periods.MapPost("/{period}/bonuses", async (
            string period,
            CreateBonusRequest request,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.AddBonusAsync(
                period,
                request,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayroll);

        periods.MapDelete("/{period}/bonuses/{bonusId:guid}", async (
            string period,
            Guid bonusId,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteBonusAsync(
                period,
                bonusId,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayroll);

        periods.MapPost("/{period}/generate", async (
            string period,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GeneratePrePayrollAsync(
                period,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayroll);

        periods.MapGet("/{period}/payslips", async (
            string period,
            Guid? employeeId,
            IPayrollService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListPayslipsAsync(period, employeeId, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(HrRead);

        periods.MapPost("/{period}/payslips/{payslipId:guid}/validate", async (
            string period,
            Guid payslipId,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ValidatePayslipAsync(
                period,
                payslipId,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayroll);

        periods.MapPost("/{period}/validate", async (
            string period,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ValidatePeriodAsync(
                period,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayrollClose);

        periods.MapPost("/{period}/close", async (
            string period,
            IPayrollService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ClosePeriodAsync(
                period,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(HrPayrollClose);
    }
}
