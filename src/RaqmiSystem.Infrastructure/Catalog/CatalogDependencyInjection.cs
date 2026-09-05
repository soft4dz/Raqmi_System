using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Catalog;

namespace RaqmiSystem.Infrastructure.Catalog;

/// <summary>
/// Enregistrement du contexte Catalogue (articles vendables). Separe de
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

        return services;
    }
}
