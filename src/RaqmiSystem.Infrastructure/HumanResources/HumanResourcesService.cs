using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.HumanResources;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.HumanResources;

/// <summary>
/// People side of the HR module: departments, positions, employee files, contracts, time entries
/// and absences.
///
/// TWO RULES SHAPE THIS SERVICE.
///
/// First, the payroll lock. Anything that would change what a CLOSED month already declared is
/// refused: time entries on a closed day, and unpaid absences overlapping a closed month. Paid
/// absence types are deliberately NOT locked - they do not alter a single figure of the payslip,
/// and refusing to record a sick leave because the month is closed would corrupt the HR file to
/// protect a number that never depended on it.
///
/// Second, personal data. Reading a full employee file exposes the identifiers protected by law
/// 18-07, so <see cref="GetEmployeeAsync"/> writes an audit entry although it is a read. The list
/// projection carries no such identifier, which is what allows browsing the directory to stay a
/// plain read.
/// </summary>
public sealed class HumanResourcesService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter) : IHumanResourcesService
{
    private const string DepartmentsEntity = "hr.departments";

    private const string PositionsEntity = "hr.positions";

    private const string EmployeesEntity = "hr.employees";

    private const string ContractsEntity = "hr.employment_contracts";

    private const string TimeEntriesEntity = "hr.time_entries";

    private const string AbsencesEntity = "hr.absences";

    public async Task<IReadOnlyCollection<DepartmentResponse>> ListDepartmentsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Department>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(department => department.IsActive);
        }

        var departments = await query
            .OrderBy(department => department.Code)
            .ToArrayAsync(cancellationToken);

        return departments.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<DepartmentResponse>> CreateDepartmentAsync(
        CreateDepartmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        Department department;

        try
        {
            department = new Department(request.Code, request.Label);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<DepartmentResponse>.Validation(ex.Message);
        }

        var exists = await dbContext.Set<Department>()
            .AnyAsync(current => current.Code == department.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<DepartmentResponse>.Conflict(
                "A department with this code already exists.");
        }

        department.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Department>().Add(department);

        try
        {
            await WriteAuditAsync(
                "hr.department.created",
                DepartmentsEntity,
                department.Id,
                context,
                new { department.Code, department.Label },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<DepartmentResponse>.Conflict(
                "A concurrent operation already created a department with this code.");
        }

        return ApplicationResult<DepartmentResponse>.Success(Map(department));
    }

    public async Task<ApplicationResult<DepartmentResponse>> UpdateDepartmentAsync(
        string code,
        UpdateDepartmentRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var department = await LoadDepartmentAsync(code, cancellationToken);

        if (department is null)
        {
            return ApplicationResult<DepartmentResponse>.NotFound("Department was not found.");
        }

        try
        {
            department.UpdateDetails(request.Label);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<DepartmentResponse>.Validation(ex.Message);
        }

        department.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.department.updated",
            DepartmentsEntity,
            department.Id,
            context,
            new { department.Code, department.Label },
            cancellationToken);

        return ApplicationResult<DepartmentResponse>.Success(Map(department));
    }

    public async Task<ApplicationResult<DepartmentResponse>> SetDepartmentActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var department = await LoadDepartmentAsync(code, cancellationToken);

        if (department is null)
        {
            return ApplicationResult<DepartmentResponse>.NotFound("Department was not found.");
        }

        // Deactivating a department that still carries active positions would leave those
        // positions pointing at a department nobody can select any more - and the employees
        // holding them would keep a job title whose department is gone.
        if (!isActive)
        {
            var activePositions = await dbContext.Set<Position>()
                .CountAsync(
                    position => position.DepartmentCode == department.Code && position.IsActive,
                    cancellationToken);

            if (activePositions > 0)
            {
                return ApplicationResult<DepartmentResponse>.Conflict(
                    $"The department still carries {activePositions} active position(s). "
                    + "Deactivate them first.");
            }
        }

        department.SetActive(isActive);
        department.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "hr.department.activated" : "hr.department.deactivated",
            DepartmentsEntity,
            department.Id,
            context,
            new { department.Code },
            cancellationToken);

        return ApplicationResult<DepartmentResponse>.Success(Map(department));
    }

    public async Task<IReadOnlyCollection<PositionResponse>> ListPositionsAsync(
        string? departmentCode,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Position>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(position => position.IsActive);
        }

        var normalizedDepartment = NormalizeOptionalCode(departmentCode);

        if (normalizedDepartment is not null)
        {
            query = query.Where(position => position.DepartmentCode == normalizedDepartment);
        }

        var rows = await query
            .Join(
                dbContext.Set<Department>().AsNoTracking(),
                position => position.DepartmentCode,
                department => department.Code,
                (position, department) => new { position, department.Label })
            .OrderBy(row => row.position.DepartmentCode)
            .ThenBy(row => row.position.Code)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => Map(row.position, row.Label)).ToArray();
    }

    public async Task<ApplicationResult<PositionResponse>> CreatePositionAsync(
        CreatePositionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        Position position;

        try
        {
            position = new Position(
                request.Code,
                request.Label,
                request.DepartmentCode,
                request.MinimumGrossSalary);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PositionResponse>.Validation(ex.Message);
        }

        var department = await dbContext.Set<Department>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == position.DepartmentCode, cancellationToken);

        if (department is null)
        {
            return ApplicationResult<PositionResponse>.NotFound("Department was not found.");
        }

        if (!department.IsActive)
        {
            return ApplicationResult<PositionResponse>.Validation(
                "Positions cannot be created in an inactive department.");
        }

        var exists = await dbContext.Set<Position>()
            .AnyAsync(current => current.Code == position.Code, cancellationToken);

        if (exists)
        {
            return ApplicationResult<PositionResponse>.Conflict("A position with this code already exists.");
        }

        position.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Position>().Add(position);

        try
        {
            await WriteAuditAsync(
                "hr.position.created",
                PositionsEntity,
                position.Id,
                context,
                new { position.Code, position.Label, position.DepartmentCode, position.MinimumGrossSalary },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<PositionResponse>.Conflict(
                "A concurrent operation already created a position with this code.");
        }

        return ApplicationResult<PositionResponse>.Success(Map(position, department.Label));
    }

    public async Task<ApplicationResult<PositionResponse>> UpdatePositionAsync(
        string code,
        UpdatePositionRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var position = await LoadPositionAsync(code, cancellationToken);

        if (position is null)
        {
            return ApplicationResult<PositionResponse>.NotFound("Position was not found.");
        }

        try
        {
            position.UpdateDetails(request.Label, request.DepartmentCode, request.MinimumGrossSalary);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<PositionResponse>.Validation(ex.Message);
        }

        var department = await dbContext.Set<Department>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == position.DepartmentCode, cancellationToken);

        if (department is null)
        {
            return ApplicationResult<PositionResponse>.NotFound("Department was not found.");
        }

        position.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.position.updated",
            PositionsEntity,
            position.Id,
            context,
            new { position.Code, position.Label, position.DepartmentCode, position.MinimumGrossSalary },
            cancellationToken);

        return ApplicationResult<PositionResponse>.Success(Map(position, department.Label));
    }

    public async Task<ApplicationResult<PositionResponse>> SetPositionActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var position = await LoadPositionAsync(code, cancellationToken);

        if (position is null)
        {
            return ApplicationResult<PositionResponse>.NotFound("Position was not found.");
        }

        if (!isActive)
        {
            var holders = await dbContext.Set<Employee>()
                .CountAsync(
                    employee => employee.PositionCode == position.Code
                        && employee.Status != EmployeeStatus.Terminated,
                    cancellationToken);

            if (holders > 0)
            {
                return ApplicationResult<PositionResponse>.Conflict(
                    $"The position is still held by {holders} employee(s). Reassign them first.");
            }
        }

        position.SetActive(isActive);
        position.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        var departmentLabel = await dbContext.Set<Department>()
            .AsNoTracking()
            .Where(department => department.Code == position.DepartmentCode)
            .Select(department => department.Label)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        await WriteAuditAsync(
            isActive ? "hr.position.activated" : "hr.position.deactivated",
            PositionsEntity,
            position.Id,
            context,
            new { position.Code },
            cancellationToken);

        return ApplicationResult<PositionResponse>.Success(Map(position, departmentLabel));
    }

    public async Task<IReadOnlyCollection<EmployeeSummaryResponse>> ListEmployeesAsync(
        string? hotelUnitCode,
        EmployeeStatus? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Employee>().AsNoTracking();

        var normalizedUnit = NormalizeOptionalCode(hotelUnitCode);

        if (normalizedUnit is not null)
        {
            query = query.Where(employee => employee.HotelUnitCode == normalizedUnit);
        }

        if (status is not null)
        {
            query = query.Where(employee => employee.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Both sides are upper-cased so the search behaves identically on PostgreSQL (which
            // compares case-sensitively) and on the SQLite test provider.
            var term = search.Trim().ToUpperInvariant();

            query = query.Where(employee =>
                employee.EmployeeNumber.ToUpper().Contains(term)
                || employee.LastName.ToUpper().Contains(term)
                || employee.FirstName.ToUpper().Contains(term));
        }

        var rows = await query
            .GroupJoin(
                dbContext.Set<Position>().AsNoTracking(),
                employee => employee.PositionCode,
                position => position.Code,
                (employee, positions) => new { employee, positions })
            .SelectMany(
                row => row.positions.DefaultIfEmpty(),
                (row, position) => new { row.employee, position })
            .Select(row => new
            {
                row.employee,
                PositionLabel = row.position != null ? row.position.Label : string.Empty,
                DepartmentCode = row.position != null ? row.position.DepartmentCode : string.Empty,
                ActiveSalary = dbContext.Set<EmploymentContract>()
                    .Where(contract => contract.EmployeeId == row.employee.Id
                        && contract.Status == ContractStatus.Active)
                    .Select(contract => (decimal?)contract.GrossSalary)
                    .FirstOrDefault()
            })
            .OrderBy(row => row.employee.LastName)
            .ThenBy(row => row.employee.FirstName)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new EmployeeSummaryResponse(
                row.employee.Id,
                row.employee.EmployeeNumber,
                row.employee.FirstName,
                row.employee.LastName,
                row.employee.HotelUnitCode,
                row.employee.PositionCode,
                row.PositionLabel,
                row.DepartmentCode,
                row.employee.Status,
                row.employee.HireDate,
                row.employee.TerminationDate,
                row.ActiveSalary))
            .ToArray();
    }

    public async Task<ApplicationResult<EmployeeResponse>> GetEmployeeAsync(
        Guid employeeId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Employee was not found.");
        }

        // Audited READ: this projection carries the national identity number, the social security
        // number and the bank account. Who consulted which file, and when, is exactly what a
        // personal-data control asks for.
        await WriteAuditAsync(
            "hr.employee.file_read",
            EmployeesEntity,
            employee.Id,
            context,
            new { employee.EmployeeNumber },
            cancellationToken);

        return ApplicationResult<EmployeeResponse>.Success(await MapDetailAsync(employee, cancellationToken));
    }

    public async Task<ApplicationResult<EmployeeResponse>> CreateEmployeeAsync(
        CreateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        Employee employee;

        try
        {
            employee = new Employee(
                request.EmployeeNumber,
                request.FirstName,
                request.LastName,
                request.HotelUnitCode,
                request.PositionCode,
                request.HireDate);

            employee.UpdateIdentity(request.FirstName, request.LastName, request.Email, request.Phone);
            employee.UpdateLegalIdentifiers(
                request.NationalIdentityNumber,
                request.SocialSecurityNumber,
                request.BankAccountNumber);
            employee.SetBadge(request.BadgeId);
            employee.SetDependentChildren(request.DependentChildren);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<EmployeeResponse>.Validation(ex.Message);
        }

        var referenceCheck = await CheckAssignmentAsync(
            employee.HotelUnitCode,
            employee.PositionCode,
            cancellationToken);

        if (referenceCheck is not null)
        {
            return referenceCheck;
        }

        var numberExists = await dbContext.Set<Employee>()
            .AnyAsync(current => current.EmployeeNumber == employee.EmployeeNumber, cancellationToken);

        if (numberExists)
        {
            return ApplicationResult<EmployeeResponse>.Conflict(
                "An employee with this employee number already exists.");
        }

        if (employee.BadgeId is not null)
        {
            var badgeTaken = await dbContext.Set<Employee>()
                .AnyAsync(current => current.BadgeId == employee.BadgeId, cancellationToken);

            if (badgeTaken)
            {
                return ApplicationResult<EmployeeResponse>.Conflict(
                    "This badge is already assigned to another employee.");
            }
        }

        employee.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<Employee>().Add(employee);

        try
        {
            // The audit details name the employee number, never the legal identifiers: an audit
            // trail that copies personal data becomes a second, unprotected store of it.
            await WriteAuditAsync(
                "hr.employee.created",
                EmployeesEntity,
                employee.Id,
                context,
                new { employee.EmployeeNumber, employee.HotelUnitCode, employee.PositionCode },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<EmployeeResponse>.Conflict(
                "A concurrent operation already created an employee with this employee number, "
                + "or the badge is already assigned.");
        }

        return ApplicationResult<EmployeeResponse>.Success(await MapDetailAsync(employee, cancellationToken));
    }

    public async Task<ApplicationResult<EmployeeResponse>> UpdateEmployeeAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .SingleOrDefaultAsync(current => current.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Employee was not found.");
        }

        try
        {
            employee.UpdateIdentity(request.FirstName, request.LastName, request.Email, request.Phone);
            employee.UpdateAssignment(request.HotelUnitCode, request.PositionCode);
            employee.UpdateLegalIdentifiers(
                request.NationalIdentityNumber,
                request.SocialSecurityNumber,
                request.BankAccountNumber);
            employee.SetBadge(request.BadgeId);
            employee.SetDependentChildren(request.DependentChildren);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<EmployeeResponse>.Validation(ex.Message);
        }

        var referenceCheck = await CheckAssignmentAsync(
            employee.HotelUnitCode,
            employee.PositionCode,
            cancellationToken);

        if (referenceCheck is not null)
        {
            return referenceCheck;
        }

        if (employee.BadgeId is not null)
        {
            var badgeTaken = await dbContext.Set<Employee>()
                .AnyAsync(
                    current => current.BadgeId == employee.BadgeId && current.Id != employee.Id,
                    cancellationToken);

            if (badgeTaken)
            {
                return ApplicationResult<EmployeeResponse>.Conflict(
                    "This badge is already assigned to another employee.");
            }
        }

        employee.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        try
        {
            await WriteAuditAsync(
                "hr.employee.updated",
                EmployeesEntity,
                employee.Id,
                context,
                new { employee.EmployeeNumber, employee.HotelUnitCode, employee.PositionCode },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<EmployeeResponse>.Conflict(
                "A concurrent operation assigned this badge to another employee.");
        }

        return ApplicationResult<EmployeeResponse>.Success(await MapDetailAsync(employee, cancellationToken));
    }

    public async Task<ApplicationResult<EmployeeResponse>> SetEmployeeSuspendedAsync(
        Guid employeeId,
        bool suspended,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .SingleOrDefaultAsync(current => current.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Employee was not found.");
        }

        try
        {
            if (suspended)
            {
                employee.Suspend();
            }
            else
            {
                employee.Reactivate();
            }
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<EmployeeResponse>.Validation(ex.Message);
        }

        employee.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            suspended ? "hr.employee.suspended" : "hr.employee.reactivated",
            EmployeesEntity,
            employee.Id,
            context,
            new { employee.EmployeeNumber },
            cancellationToken);

        return ApplicationResult<EmployeeResponse>.Success(await MapDetailAsync(employee, cancellationToken));
    }

    public async Task<ApplicationResult<EmployeeResponse>> TerminateEmployeeAsync(
        Guid employeeId,
        TerminateEmployeeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .SingleOrDefaultAsync(current => current.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Employee was not found.");
        }

        var activeContract = await dbContext.Set<EmploymentContract>()
            .SingleOrDefaultAsync(
                contract => contract.EmployeeId == employee.Id && contract.Status == ContractStatus.Active,
                cancellationToken);

        try
        {
            employee.Terminate(request.TerminationDate);

            // Terminating the employee without ending the contract would leave an active contract
            // on someone who has left - and the pre-payroll run reads active contracts.
            activeContract?.End(request.TerminationDate, request.Reason);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<EmployeeResponse>.Validation(ex.Message);
        }

        var utcNow = DateTimeOffset.UtcNow;
        employee.MarkUpdated(context.UserName, utcNow);
        activeContract?.MarkUpdated(context.UserName, utcNow);

        await WriteAuditAsync(
            "hr.employee.terminated",
            EmployeesEntity,
            employee.Id,
            context,
            new { employee.EmployeeNumber, request.TerminationDate, request.Reason },
            cancellationToken);

        return ApplicationResult<EmployeeResponse>.Success(await MapDetailAsync(employee, cancellationToken));
    }

    public async Task<ApplicationResult<IReadOnlyCollection<EmploymentContractResponse>>> ListContractsAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employeeExists = await dbContext.Set<Employee>()
            .AnyAsync(employee => employee.Id == employeeId, cancellationToken);

        if (!employeeExists)
        {
            return ApplicationResult<IReadOnlyCollection<EmploymentContractResponse>>.NotFound(
                "Employee was not found.");
        }

        var contracts = await dbContext.Set<EmploymentContract>()
            .AsNoTracking()
            .Where(contract => contract.EmployeeId == employeeId)
            .OrderByDescending(contract => contract.StartDate)
            .ToArrayAsync(cancellationToken);

        var floor = await LoadPositionFloorAsync(employeeId, cancellationToken);

        return ApplicationResult<IReadOnlyCollection<EmploymentContractResponse>>.Success(
            contracts.Select(contract => Map(contract, floor)).ToArray());
    }

    public async Task<ApplicationResult<EmploymentContractResponse>> CreateContractAsync(
        Guid employeeId,
        CreateContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<EmploymentContractResponse>.NotFound("Employee was not found.");
        }

        if (employee.Status == EmployeeStatus.Terminated)
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(
                "A terminated employee cannot receive a new contract.");
        }

        EmploymentContract contract;

        try
        {
            contract = new EmploymentContract(
                employeeId,
                request.Type,
                request.StartDate,
                request.EndDate,
                request.GrossSalary,
                request.WeeklyHours);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(ex.Message);
        }

        var floor = await dbContext.Set<Position>()
            .AsNoTracking()
            .Where(position => position.Code == employee.PositionCode)
            .Select(position => (decimal?)position.MinimumGrossSalary)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;

        if (contract.IsBelowPositionFloor(floor))
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(
                $"The gross salary is below the floor of position {employee.PositionCode} ({floor:0.00}).");
        }

        var hasActive = await dbContext.Set<EmploymentContract>()
            .AnyAsync(
                current => current.EmployeeId == employeeId && current.Status == ContractStatus.Active,
                cancellationToken);

        if (hasActive)
        {
            return ApplicationResult<EmploymentContractResponse>.Conflict(
                "The employee already holds an active contract. End it before creating a new one.");
        }

        contract.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<EmploymentContract>().Add(contract);

        try
        {
            await WriteAuditAsync(
                "hr.contract.created",
                ContractsEntity,
                contract.Id,
                context,
                new { employee.EmployeeNumber, contract.Type, contract.StartDate, contract.EndDate, contract.GrossSalary },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<EmploymentContractResponse>.Conflict(
                "A concurrent operation already created an active contract for this employee.");
        }

        return ApplicationResult<EmploymentContractResponse>.Success(Map(contract, floor));
    }

    public async Task<ApplicationResult<EmploymentContractResponse>> UpdateContractAsync(
        Guid employeeId,
        Guid contractId,
        UpdateContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Set<EmploymentContract>()
            .SingleOrDefaultAsync(
                current => current.Id == contractId && current.EmployeeId == employeeId,
                cancellationToken);

        if (contract is null)
        {
            return ApplicationResult<EmploymentContractResponse>.NotFound("Contract was not found.");
        }

        try
        {
            contract.UpdateTerms(request.GrossSalary, request.WeeklyHours, request.EndDate);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(ex.Message);
        }

        var floor = await LoadPositionFloorAsync(employeeId, cancellationToken);

        if (contract.IsBelowPositionFloor(floor))
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(
                $"The gross salary is below the floor of the position ({floor:0.00}).");
        }

        contract.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.contract.updated",
            ContractsEntity,
            contract.Id,
            context,
            new { contract.EmployeeId, contract.GrossSalary, contract.WeeklyHours, contract.EndDate },
            cancellationToken);

        return ApplicationResult<EmploymentContractResponse>.Success(Map(contract, floor));
    }

    public async Task<ApplicationResult<EmploymentContractResponse>> EndContractAsync(
        Guid employeeId,
        Guid contractId,
        EndContractRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var contract = await dbContext.Set<EmploymentContract>()
            .SingleOrDefaultAsync(
                current => current.Id == contractId && current.EmployeeId == employeeId,
                cancellationToken);

        if (contract is null)
        {
            return ApplicationResult<EmploymentContractResponse>.NotFound("Contract was not found.");
        }

        try
        {
            contract.End(request.TerminatedOn, request.Reason);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<EmploymentContractResponse>.Validation(ex.Message);
        }

        contract.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "hr.contract.ended",
            ContractsEntity,
            contract.Id,
            context,
            new { contract.EmployeeId, request.TerminatedOn, request.Reason },
            cancellationToken);

        var floor = await LoadPositionFloorAsync(employeeId, cancellationToken);

        return ApplicationResult<EmploymentContractResponse>.Success(Map(contract, floor));
    }

    public async Task<IReadOnlyCollection<TimeEntryResponse>> ListTimeEntriesAsync(
        Guid? employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<TimeEntry>()
            .AsNoTracking()
            .Where(entry => entry.WorkDate >= from && entry.WorkDate <= to);

        if (employeeId is not null)
        {
            query = query.Where(entry => entry.EmployeeId == employeeId);
        }

        var rows = await query
            .Join(
                dbContext.Set<Employee>().AsNoTracking(),
                entry => entry.EmployeeId,
                employee => employee.Id,
                (entry, employee) => new { entry, employee })
            .OrderBy(row => row.entry.WorkDate)
            .ThenBy(row => row.employee.LastName)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(row => new TimeEntryResponse(
                row.entry.Id,
                row.entry.EmployeeId,
                row.employee.EmployeeNumber,
                row.employee.FullName,
                row.entry.WorkDate,
                row.entry.HoursWorked,
                row.entry.Source,
                row.entry.Status,
                row.entry.ValidatedAt,
                row.entry.ValidatedBy))
            .ToArray();
    }

    public async Task<ApplicationResult<TimeEntryResponse>> SaveTimeEntryAsync(
        SaveTimeEntryRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<TimeEntryResponse>.NotFound("Employee was not found.");
        }

        var lockError = await FindClosedPeriodAsync(request.WorkDate, request.WorkDate, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<TimeEntryResponse>.Conflict(lockError);
        }

        var entry = await dbContext.Set<TimeEntry>()
            .SingleOrDefaultAsync(
                current => current.EmployeeId == request.EmployeeId && current.WorkDate == request.WorkDate,
                cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            if (entry is null)
            {
                entry = new TimeEntry(request.EmployeeId, request.WorkDate, request.HoursWorked, request.Source);
                entry.MarkCreated(context.UserName, utcNow);
                dbContext.Set<TimeEntry>().Add(entry);
            }
            else
            {
                entry.UpdateHours(request.HoursWorked);
                entry.MarkUpdated(context.UserName, utcNow);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return ApplicationResult<TimeEntryResponse>.Validation(ex.Message);
        }

        try
        {
            await WriteAuditAsync(
                "hr.time_entry.saved",
                TimeEntriesEntity,
                entry.Id,
                context,
                new { employee.EmployeeNumber, entry.WorkDate, entry.HoursWorked, entry.Source },
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<TimeEntryResponse>.Conflict(
                "A concurrent operation already recorded a time entry for this employee and day.");
        }

        return ApplicationResult<TimeEntryResponse>.Success(Map(entry, employee));
    }

    public async Task<ApplicationResult<TimeEntryResponse>> ValidateTimeEntryAsync(
        Guid timeEntryId,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.Set<TimeEntry>()
            .SingleOrDefaultAsync(current => current.Id == timeEntryId, cancellationToken);

        if (entry is null)
        {
            return ApplicationResult<TimeEntryResponse>.NotFound("Time entry was not found.");
        }

        var lockError = await FindClosedPeriodAsync(entry.WorkDate, entry.WorkDate, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<TimeEntryResponse>.Conflict(lockError);
        }

        try
        {
            entry.Validate(context.UserName, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return ApplicationResult<TimeEntryResponse>.Validation(ex.Message);
        }

        entry.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == entry.EmployeeId, cancellationToken);

        await WriteAuditAsync(
            "hr.time_entry.validated",
            TimeEntriesEntity,
            entry.Id,
            context,
            new { employee.EmployeeNumber, entry.WorkDate, entry.HoursWorked },
            cancellationToken);

        return ApplicationResult<TimeEntryResponse>.Success(Map(entry, employee));
    }

    public async Task<IReadOnlyCollection<AbsenceResponse>> ListAbsencesAsync(
        Guid? employeeId,
        AbsenceStatus? status,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<AbsenceRequest>().AsNoTracking();

        if (employeeId is not null)
        {
            query = query.Where(absence => absence.EmployeeId == employeeId);
        }

        if (status is not null)
        {
            query = query.Where(absence => absence.Status == status);
        }

        // Overlap, not containment: an absence starting in the previous month and ending in this
        // one concerns both.
        if (from is not null)
        {
            query = query.Where(absence => absence.EndDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(absence => absence.StartDate <= to);
        }

        var rows = await query
            .Join(
                dbContext.Set<Employee>().AsNoTracking(),
                absence => absence.EmployeeId,
                employee => employee.Id,
                (absence, employee) => new { absence, employee })
            .OrderByDescending(row => row.absence.StartDate)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row => Map(row.absence, row.employee)).ToArray();
    }

    public async Task<ApplicationResult<AbsenceResponse>> CreateAbsenceAsync(
        CreateAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Id == request.EmployeeId, cancellationToken);

        if (employee is null)
        {
            return ApplicationResult<AbsenceResponse>.NotFound("Employee was not found.");
        }

        AbsenceRequest absence;

        try
        {
            absence = new AbsenceRequest(
                request.EmployeeId,
                request.Type,
                request.StartDate,
                request.EndDate,
                request.Reason);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<AbsenceResponse>.Validation(ex.Message);
        }

        var lockError = await CheckAbsenceLockAsync(absence, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<AbsenceResponse>.Conflict(lockError);
        }

        // Overlapping absences for the same employee would double-count unpaid days and deduct
        // the same day twice from one salary.
        var overlaps = await dbContext.Set<AbsenceRequest>()
            .AnyAsync(
                current => current.EmployeeId == absence.EmployeeId
                    && current.Status != AbsenceStatus.Rejected
                    && current.Status != AbsenceStatus.Cancelled
                    && current.StartDate <= absence.EndDate
                    && current.EndDate >= absence.StartDate,
                cancellationToken);

        if (overlaps)
        {
            return ApplicationResult<AbsenceResponse>.Conflict(
                "The employee already has an absence overlapping this period.");
        }

        absence.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<AbsenceRequest>().Add(absence);

        await WriteAuditAsync(
            "hr.absence.created",
            AbsencesEntity,
            absence.Id,
            context,
            new { employee.EmployeeNumber, absence.Type, absence.StartDate, absence.EndDate },
            cancellationToken);

        return ApplicationResult<AbsenceResponse>.Success(Map(absence, employee));
    }

    public Task<ApplicationResult<AbsenceResponse>> ApproveAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return DecideAbsenceAsync(absenceId, request, AbsenceStatus.Approved, context, cancellationToken);
    }

    public Task<ApplicationResult<AbsenceResponse>> RejectAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return DecideAbsenceAsync(absenceId, request, AbsenceStatus.Rejected, context, cancellationToken);
    }

    public Task<ApplicationResult<AbsenceResponse>> CancelAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        return DecideAbsenceAsync(absenceId, request, AbsenceStatus.Cancelled, context, cancellationToken);
    }

    private async Task<ApplicationResult<AbsenceResponse>> DecideAbsenceAsync(
        Guid absenceId,
        DecideAbsenceRequest request,
        AbsenceStatus decision,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var absence = await dbContext.Set<AbsenceRequest>()
            .SingleOrDefaultAsync(current => current.Id == absenceId, cancellationToken);

        if (absence is null)
        {
            return ApplicationResult<AbsenceResponse>.NotFound("Absence was not found.");
        }

        var lockError = await CheckAbsenceLockAsync(absence, cancellationToken);

        if (lockError is not null)
        {
            return ApplicationResult<AbsenceResponse>.Conflict(lockError);
        }

        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            switch (decision)
            {
                case AbsenceStatus.Approved:
                    absence.Approve(context.UserName, utcNow, request.Note);
                    break;
                case AbsenceStatus.Rejected:
                    absence.Reject(context.UserName, utcNow, request.Note);
                    break;
                default:
                    absence.Cancel(context.UserName, utcNow, request.Note);
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApplicationResult<AbsenceResponse>.Validation(ex.Message);
        }

        absence.MarkUpdated(context.UserName, utcNow);

        var employee = await dbContext.Set<Employee>()
            .AsNoTracking()
            .SingleAsync(current => current.Id == absence.EmployeeId, cancellationToken);

        await WriteAuditAsync(
            $"hr.absence.{decision.ToString().ToLowerInvariant()}",
            AbsencesEntity,
            absence.Id,
            context,
            new { employee.EmployeeNumber, absence.Type, absence.StartDate, absence.EndDate },
            cancellationToken);

        return ApplicationResult<AbsenceResponse>.Success(Map(absence, employee));
    }

    /// <summary>
    /// The payroll lock, applied to absences. Only UNPAID absences are checked: they are the only
    /// ones that change a payslip, so a sick leave or a maternity leave can still be recorded
    /// against a closed month - the HR file must stay truthful even when the payroll of that month
    /// is frozen.
    /// </summary>
    private async Task<string?> CheckAbsenceLockAsync(
        AbsenceRequest absence,
        CancellationToken cancellationToken)
    {
        if (!absence.Type.IsUnpaid())
        {
            return null;
        }

        return await FindClosedPeriodAsync(absence.StartDate, absence.EndDate, cancellationToken);
    }

    /// <summary>
    /// Returns a message naming the first CLOSED payroll period the date range touches, or null
    /// when every month it spans is still open.
    /// </summary>
    private async Task<string?> FindClosedPeriodAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var first = PayrollMonth.FromDate(from);
        var last = PayrollMonth.FromDate(to);

        var months = new List<PayrollMonth>();

        for (var month = first; month <= last; month = month.AddMonths(1))
        {
            months.Add(month);

            // A range spanning more than a few years is a data-entry accident, not a real absence.
            // The guard keeps the IN clause bounded rather than building thousands of parameters.
            if (months.Count > 60)
            {
                break;
            }
        }

        var closed = await dbContext.Set<PayrollPeriod>()
            .AsNoTracking()
            .Where(period => months.Contains(period.Period) && period.Status == PayrollPeriodStatus.Closed)
            .OrderBy(period => period.Period)
            .Select(period => period.Period)
            .FirstOrDefaultAsync(cancellationToken);

        return closed == default
            ? null
            : $"Payroll period {closed} is closed - no further modification is allowed. "
                + "Correct it with a regularisation on an open period.";
    }

    private async Task<ApplicationResult<EmployeeResponse>?> CheckAssignmentAsync(
        string hotelUnitCode,
        string positionCode,
        CancellationToken cancellationToken)
    {
        var unit = await dbContext.Set<HotelUnit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == hotelUnitCode, cancellationToken);

        if (unit is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Hotel unit was not found.");
        }

        if (!unit.IsActive)
        {
            return ApplicationResult<EmployeeResponse>.Validation(
                "Employees cannot be assigned to an inactive hotel unit.");
        }

        var position = await dbContext.Set<Position>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == positionCode, cancellationToken);

        if (position is null)
        {
            return ApplicationResult<EmployeeResponse>.NotFound("Position was not found.");
        }

        if (!position.IsActive)
        {
            return ApplicationResult<EmployeeResponse>.Validation(
                "Employees cannot be assigned to an inactive position.");
        }

        return null;
    }

    private async Task<decimal> LoadPositionFloorAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Employee>()
            .AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Join(
                dbContext.Set<Position>().AsNoTracking(),
                employee => employee.PositionCode,
                position => position.Code,
                (_, position) => position.MinimumGrossSalary)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Department?> LoadDepartmentAsync(string code, CancellationToken cancellationToken)
    {
        string normalized;

        try
        {
            normalized = Department.NormalizeCode(code);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return await dbContext.Set<Department>()
            .SingleOrDefaultAsync(department => department.Code == normalized, cancellationToken);
    }

    private async Task<Position?> LoadPositionAsync(string code, CancellationToken cancellationToken)
    {
        string normalized;

        try
        {
            normalized = Position.NormalizeCode(code);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return await dbContext.Set<Position>()
            .SingleOrDefaultAsync(position => position.Code == normalized, cancellationToken);
    }

    private async Task<EmployeeResponse> MapDetailAsync(Employee employee, CancellationToken cancellationToken)
    {
        var position = await dbContext.Set<Position>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == employee.PositionCode, cancellationToken);

        var departmentLabel = position is null
            ? string.Empty
            : await dbContext.Set<Department>()
                .AsNoTracking()
                .Where(department => department.Code == position.DepartmentCode)
                .Select(department => department.Label)
                .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new EmployeeResponse(
            employee.Id,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.LastName,
            employee.HotelUnitCode,
            employee.PositionCode,
            position?.Label ?? string.Empty,
            position?.DepartmentCode ?? string.Empty,
            departmentLabel,
            employee.Status,
            employee.HireDate,
            employee.TerminationDate,
            employee.Email,
            employee.Phone,
            employee.NationalIdentityNumber,
            employee.SocialSecurityNumber,
            employee.BankAccountNumber,
            employee.BadgeId,
            employee.DependentChildren,
            employee.CreatedAt,
            employee.CreatedBy,
            employee.UpdatedAt,
            employee.UpdatedBy);
    }

    private static DepartmentResponse Map(Department department)
    {
        return new DepartmentResponse(
            department.Id,
            department.Code,
            department.Label,
            department.IsActive,
            department.CreatedAt,
            department.CreatedBy,
            department.UpdatedAt,
            department.UpdatedBy);
    }

    private static PositionResponse Map(Position position, string departmentLabel)
    {
        return new PositionResponse(
            position.Id,
            position.Code,
            position.Label,
            position.DepartmentCode,
            departmentLabel,
            position.MinimumGrossSalary,
            position.IsActive,
            position.CreatedAt,
            position.CreatedBy,
            position.UpdatedAt,
            position.UpdatedBy);
    }

    private static EmploymentContractResponse Map(EmploymentContract contract, decimal positionFloor)
    {
        return new EmploymentContractResponse(
            contract.Id,
            contract.EmployeeId,
            contract.Type,
            contract.StartDate,
            contract.EndDate,
            contract.GrossSalary,
            contract.WeeklyHours,
            contract.Status,
            contract.TerminatedOn,
            contract.TerminationReason,
            contract.IsBelowPositionFloor(positionFloor),
            contract.CreatedAt,
            contract.CreatedBy,
            contract.UpdatedAt,
            contract.UpdatedBy);
    }

    private static TimeEntryResponse Map(TimeEntry entry, Employee employee)
    {
        return new TimeEntryResponse(
            entry.Id,
            entry.EmployeeId,
            employee.EmployeeNumber,
            employee.FullName,
            entry.WorkDate,
            entry.HoursWorked,
            entry.Source,
            entry.Status,
            entry.ValidatedAt,
            entry.ValidatedBy);
    }

    private static AbsenceResponse Map(AbsenceRequest absence, Employee employee)
    {
        return new AbsenceResponse(
            absence.Id,
            absence.EmployeeId,
            employee.EmployeeNumber,
            employee.FullName,
            absence.Type,
            absence.Type.IsUnpaid(),
            absence.StartDate,
            absence.EndDate,
            absence.TotalDays,
            absence.Reason,
            absence.Status,
            absence.DecidedAt,
            absence.DecidedBy,
            absence.DecisionNote);
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
