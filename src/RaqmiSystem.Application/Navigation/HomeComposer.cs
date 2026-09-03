using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Composition pure de « Mon travail » à partir des clés du jeton et de l'unité du poste.
/// </summary>
/// <remarks>
/// La règle tient en une phrase : la clé de lecture compose la carte, la clé d'action donne le
/// verbe, sinon la carte est en mode Suivi ; une cible fermée bascule sur son repli, et sans
/// repli le bouton est verrouillé mais le chiffre reste lisible. Le composeur ne reçoit que des
/// clés — jamais un nom de rôle — et une clé est détenue dès qu'une des claims que le serveur
/// accepte pour elle (<see cref="PermissionRegistry.AcceptedClaims"/>) figure dans le jeton :
/// un rôle personnalisé porteur de clés cibles compose le même accueil que l'API lui accorde.
///
/// Le masquage n'est jamais une sécurité : chaque route reste gardée par sa politique, et une
/// carte composée à tort ne ferait qu'afficher « Indisponible » sur le 403 du serveur.
/// </remarks>
public static class HomeComposer
{
    public static HomeLayout Compose(IReadOnlySet<string> grantedKeys, bool hasStationUnit)
    {
        ArgumentNullException.ThrowIfNull(grantedKeys);

        bool Has(string key) => PermissionRegistry.AcceptedClaims(key).Any(grantedKeys.Contains);

        var slotsByBand = new Dictionary<HomeBand, List<HomeSlot>>
        {
            [HomeBand.Overdue] = [],
            [HomeBand.Today] = [],
            [HomeBand.Watch] = []
        };

        var unitQueuesSkippedByBand = new Dictionary<HomeBand, int>
        {
            [HomeBand.Overdue] = 0,
            [HomeBand.Today] = 0,
            [HomeBand.Watch] = 0
        };

        var showUnitLine = false;

        foreach (var queue in HomeWorkQueueCatalog.Queues)
        {
            if (!Has(queue.ReadKey))
            {
                continue;
            }

            if (queue.Scope == HomeScope.Unit)
            {
                showUnitLine = true;

                if (!hasStationUnit)
                {
                    // La route exige un code d'unité que le poste n'a pas : la carte n'est pas
                    // composée, et l'encart « poste sans unité » le dira une fois pour toutes.
                    unitQueuesSkippedByBand[queue.Band]++;
                    continue;
                }
            }

            var mode = queue.ActKey is null
                ? HomeMode.Information
                : Has(queue.ActKey) ? HomeMode.Act : HomeMode.Watch;

            var scope = queue.UnitWhenKnown && hasStationUnit ? HomeScope.Unit : queue.Scope;

            int targetTab;
            var targetLocked = false;

            if (Has(queue.TargetReadKey))
            {
                targetTab = queue.TargetTab;
            }
            else if (queue.FallbackTab is { } fallback && queue.FallbackReadKey is { } fallbackKey && Has(fallbackKey))
            {
                targetTab = fallback;
            }
            else
            {
                targetTab = queue.TargetTab;
                targetLocked = true;
            }

            slotsByBand[queue.Band].Add(new HomeSlot(queue, mode, scope, targetTab, targetLocked));
        }

        var sections = new List<HomeSection>
        {
            new(HomeSectionKind.Banner, [], HomeEmptyReason.None)
        };

        foreach (var band in new[] { HomeBand.Overdue, HomeBand.Today, HomeBand.Watch })
        {
            var slots = slotsByBand[band];

            // À faire, puis Suivi, puis Information ; à mode égal, l'ordre du registre. Le tri
            // est stable : OrderBy conserve l'ordre d'insertion à clé égale.
            var ordered = slots.OrderBy(slot => slot.Mode).ToList();

            var emptyReason = ordered.Count > 0
                ? HomeEmptyReason.None
                : unitQueuesSkippedByBand[band] > 0 ? HomeEmptyReason.UnitMissing : HomeEmptyReason.NoQueues;

            sections.Add(new HomeSection(HomeLayout.KindOf(band), ordered, emptyReason));
        }

        sections.Add(new HomeSection(HomeSectionKind.RecentScreens, [], HomeEmptyReason.None));
        sections.Add(new HomeSection(HomeSectionKind.Product, [], HomeEmptyReason.None));

        var showBusinessDate = Has(PermissionCatalog.LodgingFrontOfficeRead) && hasStationUnit;

        // Une source par appel, dans l'ordre de l'énumération (du plus léger au plus lourd). La
        // date métier est ajoutée quand le bandeau la lit, même si sa file est déjà composée.
        var sources = sections
            .SelectMany(section => section.Slots)
            .Select(slot => slot.Queue.Source)
            .Concat(showBusinessDate ? [HomeSource.BusinessDate] : Array.Empty<HomeSource>())
            .Distinct()
            .OrderBy(source => source)
            .ToList();

        var skipped = unitQueuesSkippedByBand.Values.Sum();

        return new HomeLayout(
            sections,
            sources,
            showBusinessDate,
            showUnitLine,
            ShowUnitMissingBanner: skipped > 0,
            UnitQueuesSkipped: skipped,
            ShowEstablishment: Has(PermissionCatalog.AdminSettingsRead),
            CanReadUnits: Has(PermissionCatalog.AdminUnitRead),
            CanOpenSettings: Has(PermissionCatalog.SettingsRead));
    }
}
