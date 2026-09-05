using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Infrastructure.Identity;

namespace RaqmiSystem.Infrastructure.Security;

/// <summary>
/// Enregistrements du perimetre utilisateur <-> unite (lot 2.2). Dans sa propre classe plutot
/// que dans DependencyInjection.cs : plusieurs lots de la vague 2 avancent en parallele, et
/// chacun ajoute UNE ligne dans Program.cs au lieu de se disputer le meme fichier. A rapatrier
/// dans AddRaqmiInfrastructure avec le prochain lot qui touche DependencyInjection.cs.
/// </summary>
public static class UnitScopeDependencyInjection
{
    public static IServiceCollection AddRaqmiUnitScope(this IServiceCollection services)
    {
        // Lecture du perimetre en base : ce que le jeton photographie a la connexion et au
        // refresh, et ce que les services proprietaires consulteront a la phase suivante.
        services.AddScoped<IUnitScopeProvider, UnitScopeProvider>();

        // Administration des affectations (GET/PUT /security/users/{id}/units), auditee.
        services.AddScoped<IUserUnitAssignmentService, UserUnitAssignmentService>();

        return services;
    }
}
