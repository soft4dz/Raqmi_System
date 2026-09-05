using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Infrastructure.Lodging;

namespace RaqmiSystem.Infrastructure.Catalog;

/// <summary>
/// Enregistrement de la chaine de vente : le contexte Catalogue (articles vendables) et la
/// facturation du folio (A4), qui ferme la chaine cote hotel. Separe de
/// <c>AddRaqmiInfrastructure</c> parce que ce lot a ete developpe en parallele d'autres
/// chantiers touchant DependencyInjection.cs ; a rapatrier dans AddRaqmiInfrastructure avec le
/// prochain lot qui edite ce fichier (meme precedent que IPermissionMigrationReportService dans
/// Program.cs).
/// </summary>
public static class CatalogDependencyInjection
{
    public static IServiceCollection AddRaqmiCatalog(this IServiceCollection services)
    {
        services.AddScoped<ICatalogService, CatalogService>();

        // La facturation du folio consomme IBillingService, que le coeur du PMS ne connait pas :
        // une classe dediee plutot qu'une dependance de plus sur LodgingService (voir
        // FolioInvoicingService).
        services.AddScoped<IFolioInvoicingService, FolioInvoicingService>();

        return services;
    }
}
