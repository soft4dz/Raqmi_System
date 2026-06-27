using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities;
using Raqmi.Licensing;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this WebApplication app, ServerRuntime runtime, LicenseStore licenseStore)
    {
        var group = app.MapGroup("/api/v1/sites");

        group.MapGet("/", async (HttpContext http) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var userId = EndpointAuth.GetUserId(http) ?? string.Empty;
            var roleCode = http.User.FindFirst("roleCode")?.Value ?? "user";

            if (runtime.UseDatabase && http.RequestServices.GetService<RaqmiDbContext>() is { } db)
            {
                var query = db.Sites.Where(x => x.TenantId == tenantId);
                if (!string.Equals(roleCode, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    var allowed = await db.UserSiteAssignments
                        .Where(x => x.UserId == userId)
                        .Select(x => x.SiteId)
                        .ToListAsync();
                    query = query.Where(x => allowed.Contains(x.Id));
                }

                var items = await query.OrderBy(x => x.Name)
                    .Select(x => new { id = x.Id, code = x.Code, name = x.Name, type = x.Type, city = x.City, active = x.Active })
                    .ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                var items = DemoSiteStore.ListForUser(userId, roleCode);
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("sites");

        group.MapPost("/", async (HttpContext http, SiteCreateRequest body) =>
        {
            if (!IsAdmin(http))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            var maxSites = await ResolveMaxSitesAsync(runtime, licenseStore);
            var tenantId = EndpointAuth.GetTenantId(http);
            var code = body.Code?.Trim().ToLowerInvariant() ?? string.Empty;
            var name = body.Name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                return Results.Json(new { error = "Code et nom requis" }, JsonDefaults.Options, statusCode: 400);

            if (runtime.UseDatabase && http.RequestServices.GetService<RaqmiDbContext>() is { } db)
            {
                if (await db.Sites.CountAsync(x => x.TenantId == tenantId && x.Active) >= maxSites)
                    return Results.Json(new { error = $"Limite de {maxSites} site(s) atteinte" }, JsonDefaults.Options, statusCode: 409);

                if (await db.Sites.AnyAsync(x => x.TenantId == tenantId && x.Code == code))
                    return Results.Json(new { error = "Code site déjà utilisé" }, JsonDefaults.Options, statusCode: 409);

                var site = new Site
                {
                    TenantId = tenantId,
                    Code = code,
                    Name = name,
                    Type = body.Type?.Trim().ToLowerInvariant() ?? "site",
                    City = body.City?.Trim(),
                    Active = true,
                };
                db.Sites.Add(site);
                await db.SaveChangesAsync();
                return Results.Json(new { id = site.Id, code = site.Code, name = site.Name, type = site.Type, city = site.City, active = site.Active }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                try
                {
                    var created = DemoSiteStore.Create(code, name, body.Type ?? "site", body.City, maxSites);
                    return Results.Json(DemoSiteStore.ToDto(created), JsonDefaults.Options);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 409);
                }
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("sites");

        group.MapPatch("/{id}", async (HttpContext http, string id, SitePatchRequest body) =>
        {
            if (!IsAdmin(http))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);

            if (runtime.UseDatabase && http.RequestServices.GetService<RaqmiDbContext>() is { } db)
            {
                var site = await db.Sites.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (site is null) return Results.NotFound();

                if (body.Name is not null) site.Name = body.Name.Trim();
                if (body.Type is not null) site.Type = body.Type.Trim().ToLowerInvariant();
                if (body.City is not null) site.City = string.IsNullOrWhiteSpace(body.City) ? null : body.City.Trim();
                if (body.Active.HasValue) site.Active = body.Active.Value;
                site.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Json(new { id = site.Id, code = site.Code, name = site.Name, type = site.Type, city = site.City, active = site.Active }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                var updated = DemoSiteStore.Update(id, body.Name, body.Type, body.City, body.Active);
                return updated is null ? Results.NotFound() : Results.Json(DemoSiteStore.ToDto(updated), JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("sites");
    }

    private static bool IsAdmin(HttpContext http) =>
        string.Equals(http.User.FindFirst("roleCode")?.Value, "admin", StringComparison.OrdinalIgnoreCase);

    private static async Task<int> ResolveMaxSitesAsync(ServerRuntime runtime, LicenseStore licenseStore)
    {
        if (runtime.DemoMode) return DemoData.License.Limits.MaxSites;
        var payload = await licenseStore.LoadFromDiskAsync();
        return payload?.Limits.MaxSites ?? 1;
    }
}

public sealed record SiteCreateRequest(string? Code, string? Name, string? Type, string? City);
public sealed record SitePatchRequest(string? Name, string? Type, string? City, bool? Active);
