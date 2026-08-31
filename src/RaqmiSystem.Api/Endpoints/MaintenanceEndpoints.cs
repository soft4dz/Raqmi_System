using RaqmiSystem.Application.Maintenance;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Sauvegarde &amp; restauration - volet applicatif. Trois endpoints en tout :
/// consultation (liste + etat) et declenchement d'une sauvegarde. Aucun endpoint de
/// restauration, par conception : restaurer la base de production est une procedure
/// d'administration serveur documentee (docs/deployment-onpremise.md), pas une action
/// exposable par l'API.
///
/// Permissions (constantes PermissionCatalog) : maintenance.read pour la consultation,
/// accordee a direction ; maintenance.backup pour le declenchement, reservee a
/// system.administrator, qui la recoit via le catch-all PermissionCatalog.All du seeder.
/// </summary>
internal static class MaintenanceEndpoints
{
    public static RouteGroupBuilder MapMaintenanceEndpoints(this RouteGroupBuilder api)
    {
        var backups = api.MapGroup("/maintenance/backups")
            .WithTags("Maintenance");

        backups.MapGet("", async (
            IBackupService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListBackupsAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.MaintenanceRead);

        backups.MapGet("/status", async (
            IBackupService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetStatusAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.MaintenanceRead);

        // POST sans corps : la ligne de commande de pg_dump est construite entierement
        // cote serveur (voir BackupService) - aucun parametre client n'y participe.
        backups.MapPost("/trigger", async (
            IBackupService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.TriggerBackupAsync(httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.MaintenanceBackup);

        return api;
    }
}
