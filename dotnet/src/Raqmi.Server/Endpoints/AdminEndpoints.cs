using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class AdminEndpoints
{
    private static readonly string[] RoleCodes = ["admin", "manager", "user"];

    public static void MapAdminEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        var group = app.MapGroup("/api/v1/admin");

        group.MapGet("/permissions", () =>
            Results.Json(new { items = PermissionCatalog.List() }, JsonDefaults.Options))
            .RequireRaqmiModule("administration");

        group.MapGet("/roles", (HttpContext http) =>
        {
            if (!EndpointAuth.CanReadAdmin(http, runtime))
                return Results.Json(new { error = "Accès refusé" }, JsonDefaults.Options, statusCode: 403);

            if (runtime.DemoMode)
                return Results.Json(new { items = DemoRoleStore.List() }, JsonDefaults.Options);

            return Results.Json(new { items = RoleCodes.Select(r => new { code = r, label = RoleLabel(r), isSystem = true, permissions = new[] { "*" } }) }, JsonDefaults.Options);
        }).RequireRaqmiModule("administration");

        group.MapPost("/roles", (HttpContext http, RoleCreateRequest body) =>
        {
            if (!EndpointAuth.CanWriteAdmin(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            if (!runtime.DemoMode)
                return Results.StatusCode(501);

            try
            {
                var role = DemoRoleStore.Create(body.Code ?? string.Empty, body.Label ?? string.Empty, body.Permissions ?? []);
                return Results.Json(new { code = role.Code, label = role.Label, isSystem = role.IsSystem, permissions = role.Permissions }, JsonDefaults.Options);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 409);
            }
        }).RequireRaqmiModule("administration");

        group.MapPatch("/roles/{code}", (HttpContext http, string code, RolePatchRequest body) =>
        {
            if (!EndpointAuth.CanWriteAdmin(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            if (!runtime.DemoMode)
                return Results.StatusCode(501);

            try
            {
                var updated = DemoRoleStore.Update(code, body.Label, body.Permissions);
                return updated is null
                    ? Results.NotFound()
                    : Results.Json(new { code = updated.Code, label = updated.Label, isSystem = updated.IsSystem, permissions = updated.Permissions }, JsonDefaults.Options);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 409);
            }
        }).RequireRaqmiModule("administration");

        group.MapDelete("/roles/{code}", (HttpContext http, string code) =>
        {
            if (!EndpointAuth.CanWriteAdmin(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            if (!runtime.DemoMode)
                return Results.StatusCode(501);

            try
            {
                return DemoRoleStore.Delete(code) ? Results.NoContent() : Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 409);
            }
        }).RequireRaqmiModule("administration");

        group.MapGet("/audit-logs", async (HttpContext http, string? action, string? moduleCode, string? q) =>
        {
            if (!EndpointAuth.CanReadAdmin(http, runtime))
                return Results.Json(new { error = "Accès refusé" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();

            if (runtime.UseDatabase && db is not null)
            {
                var query = db.AuditLogs.Where(x => x.TenantId == tenantId);
                if (!string.IsNullOrWhiteSpace(action))
                    query = query.Where(x => x.Action == action);
                if (!string.IsNullOrWhiteSpace(moduleCode))
                    query = query.Where(x => x.ModuleCode == moduleCode);
                if (!string.IsNullOrWhiteSpace(q))
                    query = query.Where(x => x.Description.Contains(q));

                var items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(100)
                    .Select(x => new { x.Id, x.UserId, x.Action, x.ModuleCode, x.EntityType, x.EntityId, x.Description, x.CreatedAt })
                    .ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
                return Results.Json(new { items = DemoAuditStore.List(100, action, moduleCode, q) }, JsonDefaults.Options);

            return Results.StatusCode(501);
        }).RequireRaqmiModule("administration");

        group.MapGet("/sites", async (HttpContext http) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();
            if (runtime.UseDatabase && db is not null)
            {
                var items = await db.Sites
                    .Where(x => x.TenantId == tenantId && x.Active)
                    .OrderBy(x => x.Name)
                    .Select(x => new { id = x.Id, code = x.Code, name = x.Name, type = x.Type, city = x.City, active = x.Active })
                    .ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                var items = DemoSiteStore.ListForUser(
                    EndpointAuth.GetUserId(http) ?? string.Empty,
                    EndpointAuth.GetRoleCode(http));
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("administration");

        group.MapGet("/users", async (HttpContext http) =>
        {
            if (!EndpointAuth.CanReadAdmin(http, runtime))
                return Results.Json(new { error = "Accès refusé" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();
            if (runtime.UseDatabase && db is not null)
            {
                var users = await db.Users
                    .Include(x => x.SiteAssignments)
                    .Where(x => x.TenantId == tenantId)
                    .OrderBy(x => x.FullName)
                    .ToListAsync();
                var items = users.Select(ToDto).ToList();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                IReadOnlyList<string>? scope = null;
                if (!EndpointAuth.IsAdmin(http))
                {
                    var userId = EndpointAuth.GetUserId(http);
                    scope = userId is null ? [] : DemoUserStore.GetSiteIds(tenantId, userId);
                }

                return Results.Json(new { items = DemoUserStore.ListForTenant(tenantId, scope) }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("administration");

        group.MapPost("/users", async (HttpContext http, UserCreateRequest body) =>
        {
            if (!EndpointAuth.CanWriteAdmin(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();
            var audit = http.RequestServices.GetService<AuditService>();
            var email = body.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var fullName = body.FullName?.Trim() ?? string.Empty;
            var roleCode = (body.RoleCode ?? "user").Trim().ToLowerInvariant();
            var password = body.Password ?? "demo1234";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(fullName))
                return Results.Json(new { error = "Email et nom requis" }, JsonDefaults.Options, statusCode: 400);

            if (!IsValidRole(body.RoleCode, runtime))
                return Results.Json(new { error = "Rôle invalide" }, JsonDefaults.Options, statusCode: 400);

            if (runtime.UseDatabase && db is not null)
            {
                if (await db.Users.AnyAsync(x => x.TenantId == tenantId && x.Email == email))
                    return Results.Json(new { error = "Email déjà utilisé" }, JsonDefaults.Options, statusCode: 409);

                var siteIds = await ResolveSiteIdsAsync(db, tenantId, body.SiteIds);
                if (siteIds.Count == 0)
                    return Results.Json(new { error = "Au moins un site requis" }, JsonDefaults.Options, statusCode: 400);

                var user = new User
                {
                    TenantId = tenantId,
                    Email = email,
                    FullName = fullName,
                    RoleCode = roleCode,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    Active = true,
                };
                foreach (var siteId in siteIds)
                    user.SiteAssignments.Add(new UserSiteAssignment { SiteId = siteId });

                db.Users.Add(user);
                await db.SaveChangesAsync();
                if (audit is not null)
                    await audit.LogAsync(tenantId, EndpointAuth.GetUserId(http), "create", "administration", "User", user.Id, "Utilisateur créé");
                return Results.Json(ToDto(user), JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                try
                {
                    var created = DemoUserStore.Create(tenantId, email, fullName, roleCode, password, body.SiteIds);
                    return Results.Json(DemoUserStore.ToDto(created), JsonDefaults.Options);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Json(new { error = ex.Message }, JsonDefaults.Options, statusCode: 409);
                }
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("administration");

        group.MapPatch("/users/{id}", async (HttpContext http, string id, UserPatchRequest body) =>
        {
            if (!EndpointAuth.CanWriteAdmin(http, runtime))
                return Results.Json(new { error = "Accès réservé aux administrateurs" }, JsonDefaults.Options, statusCode: 403);

            var tenantId = EndpointAuth.GetTenantId(http);
            var db = http.RequestServices.GetService<RaqmiDbContext>();

            if (body.RoleCode is { } role && !IsValidRole(role, runtime))
                return Results.Json(new { error = "Rôle invalide" }, JsonDefaults.Options, statusCode: 400);

            if (runtime.UseDatabase && db is not null)
            {
                var user = await db.Users
                    .Include(x => x.SiteAssignments)
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (user is null) return Results.NotFound();

                if (body.FullName is not null) user.FullName = body.FullName.Trim();
                if (body.RoleCode is not null) user.RoleCode = body.RoleCode.Trim().ToLowerInvariant();
                if (body.Active.HasValue) user.Active = body.Active.Value;
                if (!string.IsNullOrWhiteSpace(body.Password))
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password);

                if (body.SiteIds is not null)
                {
                    var siteIds = await ResolveSiteIdsAsync(db, tenantId, body.SiteIds);
                    if (siteIds.Count == 0)
                        return Results.Json(new { error = "Au moins un site requis" }, JsonDefaults.Options, statusCode: 400);

                    db.UserSiteAssignments.RemoveRange(user.SiteAssignments);
                    foreach (var siteId in siteIds)
                        user.SiteAssignments.Add(new UserSiteAssignment { SiteId = siteId });
                }

                user.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return Results.Json(ToDto(user), JsonDefaults.Options);
            }

            if (runtime.DemoMode)
            {
                var updated = DemoUserStore.Update(
                    tenantId,
                    id,
                    body.FullName,
                    body.RoleCode?.Trim().ToLowerInvariant(),
                    body.Active,
                    body.SiteIds,
                    body.Password);
                return updated is null ? Results.NotFound() : Results.Json(DemoUserStore.ToDto(updated), JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("administration");
    }

    private static bool IsValidRole(string? roleCode, ServerRuntime runtime)
    {
        var code = (roleCode ?? "user").Trim().ToLowerInvariant();
        if (runtime.DemoMode)
            return DemoRoleStore.Get(code) is not null;
        return RoleCodes.Contains(code);
    }

    private static async Task<List<string>> ResolveSiteIdsAsync(RaqmiDbContext db, string tenantId, IReadOnlyList<string>? siteIds)
    {
        if (siteIds is null || siteIds.Count == 0)
        {
            var defaultSite = await db.Sites.Where(x => x.TenantId == tenantId && x.Active).Select(x => x.Id).FirstOrDefaultAsync();
            return defaultSite is null ? [] : [defaultSite];
        }

        var valid = await db.Sites
            .Where(x => x.TenantId == tenantId && x.Active && siteIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();
        return valid;
    }

    private static string RoleLabel(string code) => code switch
    {
        "admin" => "Administrateur",
        "manager" => "Responsable",
        _ => "Utilisateur",
    };

    private static object ToDto(User user) => new
    {
        id = user.Id,
        email = user.Email,
        fullName = user.FullName,
        roleCode = user.RoleCode,
        active = user.Active,
        siteIds = user.SiteAssignments.Select(x => x.SiteId).ToList(),
    };
}

public sealed record UserCreateRequest(string? Email, string? FullName, string? RoleCode, string? Password, List<string>? SiteIds);
public sealed record UserPatchRequest(string? FullName, string? RoleCode, bool? Active, string? Password, List<string>? SiteIds);
public sealed record RoleCreateRequest(string? Code, string? Label, List<string>? Permissions);
public sealed record RolePatchRequest(string? Label, List<string>? Permissions);
