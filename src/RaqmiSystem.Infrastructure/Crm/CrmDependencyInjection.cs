using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

/// <summary>
/// Branchement des ports du CRM sur les modules présents dans l'installation. Séparé de
/// <c>AddRaqmiInfrastructure</c> pour que la composition dise, en un endroit, de quoi le CRM
/// dépend réellement : aujourd'hui d'une seule chose, une source de séjours.
/// </summary>
public static class CrmDependencyInjection
{
    /// <summary>
    /// Enregistre la source de séjours du CRM. Avec le PMS (<paramref name="lodgingInstalled"/>
    /// vrai, le cas de toute installation hôtelière), la vue 360 lit les réservations ; sans lui,
    /// elle s'ouvre avec un historique vide plutôt que de ne pas s'ouvrir.
    /// </summary>
    /// <remarks>
    /// <c>TryAdd</c> à dessein : un hôte qui aurait déjà choisi une autre source de séjours
    /// garde la sienne. Tant que rien n'est enregistré, <see cref="CrmService"/> se comporte
    /// comme si <see cref="NoStayHistoryReader"/> l'avait été.
    /// </remarks>
    public static IServiceCollection AddRaqmiCrmPorts(this IServiceCollection services, bool lodgingInstalled = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (lodgingInstalled)
        {
            services.TryAddScoped<IStayHistoryReader, LodgingStayHistoryReader>();
        }
        else
        {
            services.TryAddSingleton<IStayHistoryReader>(NoStayHistoryReader.Instance);
        }

        return services;
    }
}
