using RaqmiSystem.Application.Channels;

namespace RaqmiSystem.Infrastructure.Channels;

/// <summary>
/// L'annuaire des connecteurs de distribution enregistres.
///
/// IL EST VIDE AUJOURD'HUI, ET C'EST VOULU. Aucun connecteur OTA n'est livre : la couche existe
/// pour que le jour ou l'un arrive, il se branche ICI et nulle part ailleurs. Le moteur de
/// reservation continue de ne rien savoir de Booking.com ni d'Expedia, et surtout le connecteur ne
/// pourra pas apporter son propre inventaire - il publiera ce que le PMS a calcule et rejouera ce
/// que le canal a vendu par le chemin de creation normal, avec tous ses controles.
///
/// Un registre vide rend <see cref="Find"/> null pour tout code : les appelants doivent le traiter
/// comme "ce canal n'est pas branche", pas comme une erreur.
/// </summary>
public sealed class ChannelManagerRegistry(IEnumerable<IChannelManagerProvider> providers) : IChannelManagerRegistry
{
    private readonly IReadOnlyCollection<IChannelManagerProvider> providers = providers.ToArray();

    public IReadOnlyCollection<IChannelManagerProvider> Providers => providers;

    public IChannelManagerProvider? Find(string providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return null;
        }

        var normalized = providerCode.Trim().ToUpperInvariant();

        return providers.FirstOrDefault(provider =>
            string.Equals(provider.ProviderCode, normalized, StringComparison.Ordinal));
    }
}
