using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities.Finance;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        var group = app.MapGroup("/api/v1/finance");

        group.MapGet("/daily-revenue", async (HttpContext http, RaqmiDbContext? db, string? siteId, DateOnly? from, DateOnly? to) =>
        {
            if (runtime.DemoMode)
            {
                var items = DemoBusinessStore.ListDailyRevenue(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http));
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var query = db.DailyRevenueEntries.Where(x => x.TenantId == tenantId);
                if (!string.IsNullOrWhiteSpace(siteId)) query = query.Where(x => x.SiteId == siteId);
                if (from.HasValue) query = query.Where(x => x.BusinessDate >= from.Value);
                if (to.HasValue) query = query.Where(x => x.BusinessDate <= to.Value);
                var items = await query.OrderByDescending(x => x.BusinessDate).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("daily_revenue");

        group.MapPost("/daily-revenue", async (HttpContext http, RaqmiDbContext? db, AuditService? audit, DailyRevenueCreateRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateDailyRevenue(body.SiteId, body.Amount, body.Category, body.Notes, EndpointAuth.GetUserId(http));
                return Results.Json(new { id = created.Id, siteId = created.SiteId, businessDate = created.BusinessDate, amount = created.Amount, category = created.Category, status = created.Status, notes = created.Notes }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null && audit is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var entry = new DailyRevenueEntry
                {
                    TenantId = tenantId,
                    SiteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId,
                    BusinessDate = body.BusinessDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    Amount = body.Amount,
                    Category = body.Category ?? "general",
                    Status = "draft",
                    Notes = body.Notes,
                };
                db.DailyRevenueEntries.Add(entry);
                await db.SaveChangesAsync();
                await audit.LogAsync(tenantId, EndpointAuth.GetUserId(http), "create", "daily_revenue", "DailyRevenueEntry", entry.Id, "Recette créée");
                return Results.Json(entry, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("daily_revenue");

        group.MapPatch("/daily-revenue/{id}/validate", async (HttpContext http, RaqmiDbContext? db, string id) =>
        {
            if (runtime.DemoMode)
            {
                var updated = DemoBusinessStore.ValidateDailyRevenue(id);
                return updated is null ? Results.NotFound() : Results.Json(new { id = updated.Id, siteId = updated.SiteId, businessDate = updated.BusinessDate, amount = updated.Amount, category = updated.Category, status = updated.Status, notes = updated.Notes }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var entry = await db.DailyRevenueEntries.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (entry is null) return Results.NotFound();
                entry.Status = "validated";
                entry.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Json(entry, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("daily_revenue");

        group.MapGet("/invoices", async (HttpContext http, RaqmiDbContext? db, string? siteId) =>
        {
            if (runtime.DemoMode)
                return Results.Json(new { items = DemoBusinessStore.ListInvoices(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http)) }, JsonDefaults.Options);

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var items = await db.Invoices.Include(x => x.Lines).Where(x => x.TenantId == tenantId).OrderByDescending(x => x.IssueDate).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("billing");

        group.MapPost("/invoices", async (HttpContext http, RaqmiDbContext? db, InvoiceCreateRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var lines = body.Lines?.Select(l => new InvoiceLineRecord(l.Description, l.Quantity, l.UnitPrice, l.TaxRate)).ToList();
                var created = DemoBusinessStore.CreateInvoice(body.SiteId, body.ClientName, body.Number, lines, EndpointAuth.GetUserId(http));
                return Results.Json(new { id = created.Id, number = created.Number, clientName = created.ClientName, status = created.Status, issueDate = created.IssueDate, totalAmount = created.TotalAmount, lines = created.Lines }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var invoice = new Invoice
                {
                    TenantId = tenantId,
                    Number = body.Number ?? $"FAC-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                    ClientName = body.ClientName ?? "Client",
                    Status = "draft",
                    IssueDate = body.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    DueDate = body.DueDate,
                };
                foreach (var line in body.Lines ?? [])
                    invoice.Lines.Add(new InvoiceLine { Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice, TaxRate = line.TaxRate });
                invoice.TotalAmount = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice * (1 + l.TaxRate / 100));
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync();
                return Results.Json(invoice, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("billing");

        group.MapPatch("/invoices/{id}", async (HttpContext http, RaqmiDbContext? db, string id, InvoicePatchRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var updated = DemoBusinessStore.PatchInvoice(id, body.Status, body.ClientName);
                return updated is null ? Results.NotFound() : Results.Json(new { id = updated.Id, number = updated.Number, clientName = updated.ClientName, status = updated.Status, issueDate = updated.IssueDate, totalAmount = updated.TotalAmount }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var invoice = await db.Invoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (invoice is null) return Results.NotFound();
                if (body.Status is not null) invoice.Status = body.Status;
                if (body.ClientName is not null) invoice.ClientName = body.ClientName;
                invoice.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Json(invoice, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("billing");

        group.MapGet("/treasury/movements", async (HttpContext http, RaqmiDbContext? db, string? siteId) =>
        {
            if (runtime.DemoMode)
            {
                var (items, balance) = DemoBusinessStore.ListTreasury(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http));
                return Results.Json(new { items, balance }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var items = await db.TreasuryMovements.Where(x => x.TenantId == tenantId).OrderByDescending(x => x.MovementDate).ToListAsync();
                var balance = items.Sum(x => x.Type == "in" ? x.Amount : -x.Amount);
                return Results.Json(new { items, balance }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("treasury");

        group.MapPost("/treasury/movements", async (HttpContext http, RaqmiDbContext? db, TreasuryMovementRequest body) =>
        {
            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateTreasury(body.SiteId, body.Type, body.Account, body.Amount, body.Label);
                return Results.Json(new { id = created.Id, type = created.Type, account = created.Account, amount = created.Amount, movementDate = created.MovementDate, label = created.Label }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var movement = new TreasuryMovement
                {
                    TenantId = tenantId,
                    SiteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId,
                    Type = body.Type ?? "in",
                    Account = body.Account ?? "cash",
                    Amount = body.Amount,
                    MovementDate = body.MovementDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    Reference = body.Reference,
                    Label = body.Label ?? string.Empty,
                };
                db.TreasuryMovements.Add(movement);
                await db.SaveChangesAsync();
                return Results.Json(movement, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("treasury");
    }
}

public sealed record DailyRevenueCreateRequest(string? SiteId, DateOnly? BusinessDate, decimal Amount, string? Category, string? Notes);
public sealed record InvoiceLineRequest(string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
public sealed record InvoiceCreateRequest(string? SiteId, string? Number, string? ClientName, DateOnly? IssueDate, DateOnly? DueDate, List<InvoiceLineRequest>? Lines);
public sealed record InvoicePatchRequest(string? Status, string? ClientName);
public sealed record TreasuryMovementRequest(string? SiteId, string? Type, string? Account, decimal Amount, DateOnly? MovementDate, string? Reference, string? Label);
