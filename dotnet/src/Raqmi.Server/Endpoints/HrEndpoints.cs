using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities.Hr;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class HrEndpoints
{
    public static void MapHrEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        if (!runtime.UseDatabase) return;

        var group = app.MapGroup("/api/v1/hr");

        group.MapGet("/employees", async (HttpContext http, RaqmiDbContext db, string? search) =>
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
        }).RequireRaqmiModule("hr");

        group.MapPost("/employees", async (HttpContext http, RaqmiDbContext db, EmployeeCreateRequest body) =>
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
        }).RequireRaqmiModule("hr");

        group.MapGet("/employees/{id}", async (HttpContext http, RaqmiDbContext db, string id) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var employee = await db.Employees.Include(x => x.Contracts).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
            return employee is null ? Results.NotFound() : Results.Json(employee, JsonDefaults.Options);
        }).RequireRaqmiModule("hr");

        group.MapPatch("/employees/{id}", async (HttpContext http, RaqmiDbContext db, string id, EmployeePatchRequest body) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
            if (employee is null) return Results.NotFound();
            if (body.FirstName is not null) employee.FirstName = body.FirstName;
            if (body.LastName is not null) employee.LastName = body.LastName;
            if (body.Department is not null) employee.Department = body.Department;
            if (body.Status is not null) employee.Status = body.Status;
            employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Json(employee, JsonDefaults.Options);
        }).RequireRaqmiModule("hr");

        group.MapGet("/employees/{id}/contracts", async (HttpContext http, RaqmiDbContext db, string id) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var items = await db.EmploymentContracts.Where(x => x.EmployeeId == id && x.Employee.TenantId == tenantId).ToListAsync();
            return Results.Json(new { items }, JsonDefaults.Options);
        }).RequireRaqmiModule("hr");

        group.MapPost("/employees/{id}/contracts", async (HttpContext http, RaqmiDbContext db, string id, ContractCreateRequest body) =>
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
        }).RequireRaqmiModule("hr");

        group.MapGet("/attendance", async (HttpContext http, RaqmiDbContext db, DateOnly? date) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var items = await db.AttendanceRecords.Include(x => x.Employee)
                .Where(x => x.TenantId == tenantId && x.WorkDate == d)
                .ToListAsync();
            return Results.Json(new { items }, JsonDefaults.Options);
        }).RequireRaqmiModule("hr");

        group.MapPost("/attendance", async (HttpContext http, RaqmiDbContext db, AttendanceCreateRequest body) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var record = new AttendanceRecord
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
        }).RequireRaqmiModule("hr");
    }
}

public sealed record EmployeeCreateRequest(string? Matricule, string? FirstName, string? LastName, string? SiteId, string? Department, DateOnly? HireDate);
public sealed record EmployeePatchRequest(string? FirstName, string? LastName, string? Department, string? Status);
public sealed record ContractCreateRequest(string? ContractType, DateOnly? StartDate, DateOnly? EndDate, decimal BaseSalary);
public sealed record AttendanceCreateRequest(string? EmployeeId, string? SiteId, DateOnly? WorkDate, TimeOnly? CheckIn, TimeOnly? CheckOut, string? Notes);
