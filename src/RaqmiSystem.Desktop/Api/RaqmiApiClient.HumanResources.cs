using System.Globalization;
using System.Net.Http;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Desktop.Api;

// Module Ressources humaines : appels des groupes /api/v1/hr/*. Fichier de classe partielle,
// pour que ce chantier n'entre pas en conflit avec les autres modules qui alimentent le meme
// client API.
public sealed partial class RaqmiApiClient
{
    private const string HrDepartmentsPath = "/api/v1/hr/departments";

    private const string HrPositionsPath = "/api/v1/hr/positions";

    private const string HrEmployeesPath = "/api/v1/hr/employees";

    private const string HrTimeEntriesPath = "/api/v1/hr/time-entries";

    private const string HrAbsencesPath = "/api/v1/hr/absences";

    private const string HrPayrollPeriodsPath = "/api/v1/hr/payroll/periods";

    private const string HrPayrollParametersPath = "/api/v1/hr/payroll/parameters";

    public async Task<IReadOnlyCollection<DepartmentResponse>> GetHrDepartmentsAsync(
        string apiBaseUrl,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = includeInactive ? $"{HrDepartmentsPath}?includeInactive=true" : HrDepartmentsPath;
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<DepartmentResponse>>(response, cancellationToken);
    }

    public async Task<DepartmentResponse> CreateHrDepartmentAsync(
        string apiBaseUrl,
        CreateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrDepartmentsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<DepartmentResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PositionResponse>> GetHrPositionsAsync(
        string apiBaseUrl,
        string? departmentCode = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(departmentCode))
        {
            query.Add($"departmentCode={Uri.EscapeDataString(departmentCode)}");
        }

        if (includeInactive)
        {
            query.Add("includeInactive=true");
        }

        var path = query.Count == 0 ? HrPositionsPath : $"{HrPositionsPath}?{string.Join("&", query)}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PositionResponse>>(response, cancellationToken);
    }

    public async Task<PositionResponse> CreateHrPositionAsync(
        string apiBaseUrl,
        CreatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrPositionsPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PositionResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Annuaire des collaborateurs. La projection de liste ne porte volontairement aucun
    /// identifiant legal (NIN, NSS, RIB) : ces donnees ne sont servies que par la fiche detaillee,
    /// dont la consultation est journalisee.
    /// </summary>
    public async Task<IReadOnlyCollection<EmployeeSummaryResponse>> GetHrEmployeesAsync(
        string apiBaseUrl,
        string? hotelUnitCode = null,
        EmployeeStatus? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(hotelUnitCode))
        {
            query.Add($"hotelUnitCode={Uri.EscapeDataString(hotelUnitCode)}");
        }

        if (status is not null)
        {
            query.Add($"status={status}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        var path = query.Count == 0 ? HrEmployeesPath : $"{HrEmployeesPath}?{string.Join("&", query)}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<EmployeeSummaryResponse>>(response, cancellationToken);
    }

    /// <summary>
    /// Fiche complete d'un collaborateur, identifiants legaux compris. Chaque appel ecrit une
    /// entree d'audit cote serveur : consulter ces donnees est un acte sensible.
    /// </summary>
    public async Task<EmployeeResponse> GetHrEmployeeAsync(
        string apiBaseUrl,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{HrEmployeesPath}/{employeeId}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmployeeResponse>(response, cancellationToken);
    }

    public async Task<EmployeeResponse> CreateHrEmployeeAsync(
        string apiBaseUrl,
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrEmployeesPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmployeeResponse>(response, cancellationToken);
    }

    public async Task<EmployeeResponse> UpdateHrEmployeeAsync(
        string apiBaseUrl,
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Put, $"{HrEmployeesPath}/{employeeId}", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmployeeResponse>(response, cancellationToken);
    }

    public async Task<EmployeeResponse> SetHrEmployeeSuspendedAsync(
        string apiBaseUrl,
        Guid employeeId,
        bool suspended,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var action = suspended ? "suspend" : "reactivate";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrEmployeesPath}/{employeeId}/{action}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmployeeResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Met fin a la relation de travail : le dossier passe en Termine et le contrat actif est
    /// cloture a la meme date. Le collaborateur reste paye pour le mois de son depart.
    /// </summary>
    public async Task<EmployeeResponse> TerminateHrEmployeeAsync(
        string apiBaseUrl,
        Guid employeeId,
        TerminateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrEmployeesPath}/{employeeId}/terminate", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmployeeResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<EmploymentContractResponse>> GetHrContractsAsync(
        string apiBaseUrl,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{HrEmployeesPath}/{employeeId}/contracts", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<EmploymentContractResponse>>(response, cancellationToken);
    }

    public async Task<EmploymentContractResponse> CreateHrContractAsync(
        string apiBaseUrl,
        Guid employeeId,
        CreateContractRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrEmployeesPath}/{employeeId}/contracts", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<EmploymentContractResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TimeEntryResponse>> GetHrTimeEntriesAsync(
        string apiBaseUrl,
        DateOnly from,
        DateOnly to,
        Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = $"from={FormatDate(from)}&to={FormatDate(to)}";

        if (employeeId is not null)
        {
            query += $"&employeeId={employeeId}";
        }

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{HrTimeEntriesPath}?{query}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<TimeEntryResponse>>(response, cancellationToken);
    }

    public async Task<TimeEntryResponse> SaveHrTimeEntryAsync(
        string apiBaseUrl,
        SaveTimeEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrTimeEntriesPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<TimeEntryResponse>(response, cancellationToken);
    }

    public async Task<TimeEntryResponse> ValidateHrTimeEntryAsync(
        string apiBaseUrl,
        Guid timeEntryId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrTimeEntriesPath}/{timeEntryId}/validate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<TimeEntryResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AbsenceResponse>> GetHrAbsencesAsync(
        string apiBaseUrl,
        Guid? employeeId = null,
        AbsenceStatus? status = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var query = new List<string>();

        if (employeeId is not null)
        {
            query.Add($"employeeId={employeeId}");
        }

        if (status is not null)
        {
            query.Add($"status={status}");
        }

        if (from is not null)
        {
            query.Add($"from={FormatDate(from.Value)}");
        }

        if (to is not null)
        {
            query.Add($"to={FormatDate(to.Value)}");
        }

        var path = query.Count == 0 ? HrAbsencesPath : $"{HrAbsencesPath}?{string.Join("&", query)}";
        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<AbsenceResponse>>(response, cancellationToken);
    }

    public async Task<AbsenceResponse> CreateHrAbsenceAsync(
        string apiBaseUrl,
        CreateAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrAbsencesPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<AbsenceResponse>(response, cancellationToken);
    }

    public async Task<AbsenceResponse> ApproveHrAbsenceAsync(
        string apiBaseUrl,
        Guid absenceId,
        DecideAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrAbsencesPath}/{absenceId}/approve", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<AbsenceResponse>(response, cancellationToken);
    }

    public async Task<AbsenceResponse> RejectHrAbsenceAsync(
        string apiBaseUrl,
        Guid absenceId,
        DecideAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrAbsencesPath}/{absenceId}/reject", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<AbsenceResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PayrollParameterSetResponse>> GetPayrollParameterSetsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, HrPayrollParametersPath, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PayrollParameterSetResponse>>(response, cancellationToken);
    }

    public async Task<PayrollParameterSetResponse> CreatePayrollParameterSetAsync(
        string apiBaseUrl,
        CreatePayrollParameterSetRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, HrPayrollParametersPath, request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollParameterSetResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PayrollPeriodResponse>> GetPayrollPeriodsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, HrPayrollPeriodsPath, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PayrollPeriodResponse>>(response, cancellationToken);
    }

    public async Task<PayrollPeriodResponse> GetPayrollPeriodAsync(
        string apiBaseUrl,
        string period,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollPeriodResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PayrollBonusResponse>> GetPayrollBonusesAsync(
        string apiBaseUrl,
        string period,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/bonuses", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PayrollBonusResponse>>(response, cancellationToken);
    }

    public async Task<PayrollBonusResponse> AddPayrollBonusAsync(
        string apiBaseUrl,
        string period,
        CreateBonusRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/bonuses", request, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollBonusResponse>(response, cancellationToken);
    }

    public async Task<PayrollBonusResponse> DeletePayrollBonusAsync(
        string apiBaseUrl,
        string period,
        Guid bonusId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Delete, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/bonuses/{bonusId}", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollBonusResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Lance la pre-paie du mois. Operation idempotente cote serveur : les bulletins deja valides
    /// ne sont pas recalcules, ils sont comptes dans SkippedValidated.
    /// </summary>
    public async Task<PrePayrollRunResponse> GeneratePrePayrollAsync(
        string apiBaseUrl,
        string period,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/generate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PrePayrollRunResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PayslipResponse>> GetPayslipsAsync(
        string apiBaseUrl,
        string period,
        Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var path = $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/payslips";

        if (employeeId is not null)
        {
            path += $"?employeeId={employeeId}";
        }

        var response = await SendAsync(apiBaseUrl, HttpMethod.Get, path, null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<IReadOnlyCollection<PayslipResponse>>(response, cancellationToken);
    }

    public async Task<PayslipResponse> ValidatePayslipAsync(
        string apiBaseUrl,
        string period,
        Guid payslipId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/payslips/{payslipId}/validate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayslipResponse>(response, cancellationToken);
    }

    public async Task<PayrollPeriodResponse> ValidatePayrollPeriodAsync(
        string apiBaseUrl,
        string period,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/validate", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollPeriodResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Cloture definitivement la periode. Irreversible : apres cet appel, plus aucun bulletin,
    /// prime, pointage ou absence sans solde de ce mois ne peut etre modifie.
    /// </summary>
    public async Task<PayrollPeriodResponse> ClosePayrollPeriodAsync(
        string apiBaseUrl,
        string period,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var response = await SendAsync(apiBaseUrl, HttpMethod.Post, $"{HrPayrollPeriodsPath}/{Uri.EscapeDataString(period)}/close", null, includeAuthorization: true, cancellationToken);

        return await ReadResponseAsync<PayrollPeriodResponse>(response, cancellationToken);
    }

    private static string FormatDate(DateOnly value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
