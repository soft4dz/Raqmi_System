using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities.Stocks;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class StockEndpoints
{
    public static void MapStockEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        if (!runtime.UseDatabase) return;

        var group = app.MapGroup("/api/v1/stocks");

        group.MapGet("/products", async (HttpContext http, RaqmiDbContext db) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var items = await db.Products.Where(x => x.TenantId == tenantId && x.Active).OrderBy(x => x.Name).ToListAsync();
            return Results.Json(new { items }, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapPost("/products", async (HttpContext http, RaqmiDbContext db, ProductCreateRequest body) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var product = new Product
            {
                TenantId = tenantId,
                Code = body.Code ?? $"P-{Guid.NewGuid().ToString()[..6]}",
                Name = body.Name ?? string.Empty,
                Unit = body.Unit ?? "u",
                MinStockLevel = body.MinStockLevel,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Json(product, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapGet("/movements", async (HttpContext http, RaqmiDbContext db, string? productId) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var query = db.StockMovements.Include(x => x.Product).Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(productId)) query = query.Where(x => x.ProductId == productId);
            var items = await query.OrderByDescending(x => x.MovementDate).ToListAsync();
            var stockByProduct = await db.StockMovements.Where(x => x.TenantId == tenantId)
                .GroupBy(x => x.ProductId)
                .Select(g => new { productId = g.Key, quantity = g.Sum(m => m.Type == "in" ? m.Quantity : m.Type == "out" ? -m.Quantity : m.Quantity) })
                .ToListAsync();
            return Results.Json(new { items, stockByProduct }, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapPost("/movements", async (HttpContext http, RaqmiDbContext db, StockMovementRequest body) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var movement = new StockMovement
            {
                TenantId = tenantId,
                ProductId = body.ProductId ?? string.Empty,
                SiteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId,
                Type = body.Type ?? "in",
                Quantity = body.Quantity,
                MovementDate = body.MovementDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Reference = body.Reference,
            };
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync();
            return Results.Json(movement, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapGet("/inventories", async (HttpContext http, RaqmiDbContext db) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var items = await db.InventorySessions.Include(x => x.Lines).Where(x => x.TenantId == tenantId).OrderByDescending(x => x.SessionDate).ToListAsync();
            return Results.Json(new { items }, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapPost("/inventories", async (HttpContext http, RaqmiDbContext db, InventoryCreateRequest body) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var siteId = body.SiteId ?? RaqmiDbSeeder.DemoSiteId;
            var session = new InventorySession
            {
                TenantId = tenantId,
                SiteId = siteId,
                SessionDate = body.SessionDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                Status = "open",
            };
            var products = await db.Products.Where(x => x.TenantId == tenantId && x.Active).ToListAsync();
            foreach (var product in products)
            {
                var expected = await db.StockMovements.Where(m => m.TenantId == tenantId && m.ProductId == product.Id && m.SiteId == siteId)
                    .SumAsync(m => m.Type == "in" ? m.Quantity : m.Type == "out" ? -m.Quantity : m.Quantity);
                session.Lines.Add(new InventoryLine { ProductId = product.Id, ExpectedQty = expected, CountedQty = expected });
            }
            db.InventorySessions.Add(session);
            await db.SaveChangesAsync();
            return Results.Json(session, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");

        group.MapPost("/inventories/{id}/close", async (HttpContext http, RaqmiDbContext db, string id) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var session = await db.InventorySessions.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
            if (session is null) return Results.NotFound();
            session.Status = "closed";
            await db.SaveChangesAsync();
            return Results.Json(session, JsonDefaults.Options);
        }).RequireRaqmiModule("stocks");
    }
}

public sealed record ProductCreateRequest(string? Code, string? Name, string? Unit, decimal MinStockLevel);
public sealed record StockMovementRequest(string? ProductId, string? SiteId, string? Type, decimal Quantity, DateOnly? MovementDate, string? Reference);
public sealed record InventoryCreateRequest(string? SiteId, DateOnly? SessionDate);
