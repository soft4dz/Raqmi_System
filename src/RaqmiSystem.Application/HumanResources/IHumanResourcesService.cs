using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.HumanResources;

namespace RaqmiSystem.Application.HumanResources;

/// <summary>
/// People side of the HR module: the position reference data, employee files, employment
/// contracts, daily time entries and absences. Payroll itself lives in
/// <see cref="IPayrollService"/>, which consumes what this service records.
///
/// The split is the one the closing lock draws. Everything here can be corrected as long as the
/// payroll period it feeds is still open; once that period is closed, the writes that would
/// change what was already declared are refused - the guard lives in the implementation, next to
/// the payroll period it has to read.
/// </summary>
public interface IHumanResourcesService
{
    Task<IReadOnlyCollection<DepartmentResponse>> ListDepartmentsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepartmentResponse>> CreateDepartmentAsync(
        CreateDepartmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepartmentResponse>> UpdateDepartmentAsync(
        string code,
        UpdateDepartmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DepartmentResponse>> SetDepartmentActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PositionResponse>> ListPositionsAsync(
        string? departmentCode,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PositionResponse>> CreatePositionAsync(
        CreatePositionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PositionResponse>> UpdatePositionAsync(
        string code,
        UpdatePositionRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<PositionResponse>> SetPositionActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EmployeeSummaryResponse>> ListEmployeesAsync(
        string? hotelUnitCode,
        EmployeeStatus? status,
        string? search,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the full employee file, legal identifiers included. Takes an
    /// <see cref="OperationContext"/> although it is a read: exposing personal data protected by
    /// law 18-07 is itself a sensitive act, so this call writes an audit entry naming who
    /// consulted which file.
    /// </summary>
    Task<ApplicationResult<EmployeeResponse>> GetEmployeeAsync(
        Guid employeeId,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmployeeResponse>> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmployeeResponse>> UpdateEmployeeAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmployeeResponse>> SetEmployeeSuspendedAsync(
        Guid employeeId,
        bool suspended,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmployeeResponse>> TerminateEmployeeAsync(
        Guid employeeId,
        TerminateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyCollection<EmploymentContractResponse>>> ListContractsAsync(
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmploymentContractResponse>> CreateContractAsync(
        Guid employeeId,
        CreateContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmploymentContractResponse>> UpdateContractAsync(
        Guid employeeId,
        Guid contractId,
        UpdateContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<EmploymentContractResponse>> EndContractAsync(
        Guid employeeId,
        Guid contractId,
        EndContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TimeEntryResponse>> ListTimeEntriesAsync(
        Guid? employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TimeEntryResponse>> SaveTimeEntryAsync(
        SaveTimeEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TimeEntryResponse>> ValidateTimeEntryAsync(
        Guid timeEntryId,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AbsenceResponse>> ListAbsencesAsync(
        Guid? employeeId,
        AbsenceStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AbsenceResponse>> CreateAbsenceAsync(
        CreateAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AbsenceResponse>> ApproveAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AbsenceResponse>> RejectAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<AbsenceResponse>> CancelAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
