using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities.Hr;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class HrEndpoints
{
    public static void MapHrEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        var group = app.MapGroup("/api/v1/hr");

        group.MapGet("/employees", async (HttpContext http, RaqmiDbContext? db, string? siteId, string? search) =>
        {
            if (runtime.DemoMode)
                return Results.Json(new { items = DemoBusinessStore.ListEmployees(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http), search) }, JsonDefaults.Options);

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var query = db.Employees.Where(x => x.TenantId == tenantId);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    query = query.Where(x => x.FirstName.ToLower().Contains(s) || x.LastName.ToLower().Contains(s) || x.Matricule.ToLower().Contains(s));
                }
                var items = await query.OrderBy(x => x.LastName).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");

        group.MapPost("/employees", async (HttpContext http, RaqmiDbContext? db, EmployeeCreateRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateEmployee(body.SiteId, body.FirstName, body.LastName, body.Department, body.Matricule, EndpointAuth.GetUserId(http));
                return Results.Json(new { id = created.Id, matricule = created.Matricule, firstName = created.FirstName, lastName = created.LastName, department = created.Department, status = created.Status }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var employee = new Employee
                {
                    TenantId = tenantId,
                    Matricule = body.Matricule ?? $"EMP-{Guid.NewGuid().ToString()[..8]}",
                    FirstName = body.FirstName ?? string.Empty,
                    LastName = body.LastName ?? string.Empty,
                    SiteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId,
                    Department = body.Department ?? string.Empty,
                    HireDate = body.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                };
                db.Employees.Add(employee);
                await db.SaveChangesAsync();
                return Results.Json(employee, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");

        group.MapGet("/employees/{id}/contracts", async (HttpContext http, RaqmiDbContext? db, string id) =>
        {
            if (runtime.DemoMode)
                return Results.Json(new { items = DemoBusinessStore.ListContracts(id) }, JsonDefaults.Options);

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var items = await db.EmploymentContracts.Where(x => x.EmployeeId == id && x.Employee.TenantId == tenantId).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");

        group.MapPost("/employees/{id}/contracts", async (HttpContext http, RaqmiDbContext? db, string id, ContractCreateRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateContract(id, body.ContractType, body.BaseSalary, body.StartDate);
                return Results.Json(new { id = created.Id, contractType = created.ContractType, startDate = created.StartDate, baseSalary = created.BaseSalary }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (employee is null) return Results.NotFound();
                var contract = new EmploymentContract
                {
                    EmployeeId = id,
                    ContractType = body.ContractType ?? "cdi",
                    StartDate = body.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    EndDate = body.EndDate,
                    BaseSalary = body.BaseSalary,
                };
                db.EmploymentContracts.Add(contract);
                await db.SaveChangesAsync();
                return Results.Json(contract, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");

        group.MapGet("/attendance", async (HttpContext http, RaqmiDbContext? db, string? siteId, DateOnly? date) =>
        {
            if (runtime.DemoMode)
                return Results.Json(new { items = DemoBusinessStore.ListAttendance(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http), date) }, JsonDefaults.Options);

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var items = await db.AttendanceRecords.Include(x => x.Employee).Where(x => x.TenantId == tenantId && x.WorkDate == d).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");

        group.MapPost("/attendance", async (HttpContext http, RaqmiDbContext? db, AttendanceCreateRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateAttendance(body.SiteId, body.EmployeeId, body.WorkDate, body.CheckIn?.ToString("HH:mm"), body.CheckOut?.ToString("HH:mm"), body.Notes);
                return Results.Json(new { id = created.Id, employeeId = created.EmployeeId, workDate = created.WorkDate, checkIn = created.CheckIn, checkOut = created.CheckOut, notes = created.Notes }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var record = new Raqmi.Data.Entities.Hr.AttendanceRecord
                {
                    TenantId = tenantId,
                    EmployeeId = body.EmployeeId ?? string.Empty,
                    SiteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId,
                    WorkDate = body.WorkDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    CheckIn = body.CheckIn,
                    CheckOut = body.CheckOut,
                    Notes = body.Notes,
                };
                db.AttendanceRecords.Add(record);
                await db.SaveChangesAsync();
                return Results.Json(record, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("hr");
    }
}

public sealed record EmployeeCreateRequest(string? Matricule, string? FirstName, string? LastName, string? SiteId, string? Department, DateOnly? HireDate);
public sealed record ContractCreateRequest(string? ContractType, DateOnly? StartDate, DateOnly? EndDate, decimal BaseSalary);
public sealed record AttendanceCreateRequest(string? EmployeeId, string? SiteId, DateOnly? WorkDate, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Notes);
