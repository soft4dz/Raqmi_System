namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Mise en bande des cartes APRÈS projection : la bande affichée est celle que la carte porte
/// — celle du registre, ou celle qu'un booléen serveur a imposée — jamais celle que le registre
/// déclarait avant l'appel.
/// </summary>
/// <remarks>
/// Le registre range <c>backup</c> dans « En retard » ; quand le serveur répond
/// <c>IsOverdue = false</c>, la projection la déplace vers « À surveiller » et la renomme
/// « Dernière sauvegarde ». Sans cette re-répartition, l'en-tête et le compteur d'une bande
/// contrediraient la synthèse du bandeau, qui compte déjà la bande projetée : l'écran
/// qualifierait de « dépassé » ce que le serveur déclare à jour.
///
/// L'ordre est celui de la charte, appliqué à la bande d'ARRIVÉE : À faire, puis Suivi, puis
/// Information ; à mode égal, l'ordre du registre. Une carte masquée (zéro hors « Aujourd'hui »,
/// journée à clôturer sans retard) n'est dans aucune bande.
/// </remarks>
public static class HomeBandPlacement
{
    // Rang de chaque file dans le registre : le départage à mode égal ne peut pas dépendre de
    // l'ordre d'énumération d'un dictionnaire de la vue.
    private static readonly IReadOnlyDictionary<string, int> RegistryOrder =
        HomeWorkQueueCatalog.Queues
            .Select((queue, index) => (queue.Id, Index: index))
            .ToDictionary(pair => pair.Id, pair => pair.Index, StringComparer.Ordinal);

    /// <summary>Les cartes visibles de cette bande, dans l'ordre de rendu.</summary>
    public static IReadOnlyList<HomeCard> InBand(IEnumerable<HomeCard> cards, HomeBand band)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return
        [
            .. cards
                .Where(card => !card.IsHidden && card.Band == band)
                .OrderBy(card => card.Slot.Mode)
                .ThenBy(card => RegistryOrder.TryGetValue(card.Slot.Queue.Id, out var index) ? index : int.MaxValue)
        ];
    }
}
