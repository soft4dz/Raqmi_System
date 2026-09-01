using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Security;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module "Administration et utilisateurs". The permission catalog, the user listing and the
/// password reset used to be declared inline in Program.cs; they moved here on the same routes,
/// under the same authorization policies, and were joined by the rest of the module - so this
/// module follows the same one-file-per-module convention as SettingsEndpoints or BillingEndpoints.
///
/// The endpoints carry no business rule of their own: the anti-lockout guards live in
/// <see cref="IUserAdministrationService"/>, where no caller can go around them.
/// </summary>
internal static class SecurityEndpoints
{
    public static RouteGroupBuilder MapSecurityEndpoints(this RouteGroupBuilder api)
    {
        var security = api.MapGroup("/security")
            .WithTags("Security");

        security.MapGet("/permissions", () =>
        {
            var permissions = PermissionCatalog.All
                .Select(permission => new PermissionSummary(
                    permission.Key,
                    permission.Name,
                    permission.Category,
                    permission.Description))
                .OrderBy(permission => permission.Category)
                .ThenBy(permission => permission.Key)
                .ToArray();

            return Results.Ok(permissions);
        }).RequireAuthorization(PermissionCatalog.UsersRead);

        security.MapGet("/roles", async (
            IUserAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var roles = await service.ListRolesAsync(cancellationToken);
            return Results.Ok(roles);
        }).RequireAuthorization(PermissionCatalog.UsersRead);

        // Rapport de migration des roles PERSONNALISES vers le modele domaine.ressource.action
        // (lot 2.1). Lecture seule ; il precede toute modification d'un role personnalise, que le
        // seeder ne touche jamais. Protege par roles.read : c'est un etat des roles, pas des
        // utilisateurs - et roles.read vaut admin.role.read par le registre.
        security.MapGet("/permission-migration-report", async (
            IPermissionMigrationReportService service,
            CancellationToken cancellationToken) =>
        {
            var report = await service.BuildAsync(cancellationToken);
            return Results.Ok(report);
        }).RequireAuthorization(PermissionCatalog.RolesRead);

        MapUserEndpoints(security);

        return api;
    }

    private static void MapUserEndpoints(RouteGroupBuilder security)
    {
        var users = security.MapGroup("/users");

        // Same route, same policy, same ordering, and every field the previous inline listing
        // returned (id, userName, email, displayName, isActive, mustChangePassword) under the
        // same names - plus the three an administration screen cannot work without: last login,
        // lockout state, and the roles held. Purely additive for an existing consumer.
        //
        // The one deliberate change of behaviour: deactivated accounts are now hidden unless
        // includeInactive is asked for, which is how every other listing in this API behaves
        // (hotel units, customers).
        users.MapGet("", async (
            bool? includeInactive,
            string? search,
            IUserAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListAsync(includeInactive == true, search, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.UsersRead);

        users.MapGet("/{id:guid}", async (
            Guid id,
            IUserAdministrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersRead);

        users.MapPost("", async (
            CreateUserRequest request,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/security/users/{result.Value.User.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPost("/{id:guid}/activate", async (
            Guid id,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetActiveAsync(id, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetActiveAsync(id, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPut("/{id:guid}/roles", async (
            Guid id,
            SetUserRolesRequest request,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            // This endpoint REPLACES the role set, so an absent "roles" field would otherwise be
            // read as "strip every role" - too destructive an interpretation of a malformed body.
            // Stripping every role stays possible, but only by asking for it with an empty array.
            if (request.Roles is null)
            {
                return Results.BadRequest(new ErrorResponse(
                    "The roles field is required. Send an empty array to remove every role."));
            }

            var result = await service.SetRolesAsync(
                id,
                request.Roles,
                httpContext.ToOperationContext(),
                cancellationToken);

            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPost("/{id:guid}/unlock", async (
            Guid id,
            IUserAdministrationService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UnlockAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.UsersWrite);

        users.MapPost("/{id:guid}/reset-password", async (
            Guid id,
            RaqmiDbContext db,
            IPasswordHasher passwordHasher,
            IAuditLogWriter auditLogWriter,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var user = await db.Users.SingleOrDefaultAsync(current => current.Id == id, cancellationToken);

            if (user is null)
            {
                return Results.NotFound(new ErrorResponse("User was not found."));
            }

            // There is no email/SMTP infrastructure in this repository yet, so there is no channel to
            // deliver the temporary password other than this response. It is generated with a CSPRNG,
            // hashed before being persisted, never written to the audit log, and the account is flagged
            // MustChangePassword so it cannot be reused past the administrator's first hand-off.
            var temporaryPassword = TemporaryPasswordGenerator.Generate();
            user.SetPasswordHash(passwordHasher.Hash(temporaryPassword), mustChangePassword: true);

            await db.SaveChangesAsync(cancellationToken);

            var context = httpContext.ToOperationContext();

            await auditLogWriter.WriteAsync(
                new AuditLogEntry(context.UserId, context.UserName, "security.user.password_reset", "security.users", user.Id.ToString(), context.IpAddress, null),
                cancellationToken);

            return Results.Ok(new ResetPasswordResponse(temporaryPassword));
        }).RequireAuthorization(PermissionCatalog.UsersWrite);
    }
}
