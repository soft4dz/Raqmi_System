using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

// Une puce de domaine du catalogue de l'accueil. Le libelle affiche est le libelle COURT
// du catalogue (le meme que l'en-tete de la barre laterale : un seul vocabulaire sous deux
// densites) ; le nom complet reste dans l'info-bulle et dans le fil « Domaine → modules ».
public sealed record FunctionalDomainOption(
    string? Id,
    string Name,
    string ShortLabel,
    int ModuleCount,
    int AvailableCount)
{
    public string DisplayLabel => Id is null
        ? $"Tous les domaines · {ModuleCount} modules"
        : $"{Id} · {ShortLabel}  ({AvailableCount}/{ModuleCount})";

    // « 06 · PMS / Hébergement » : le nom officiel, avec son numero, a un survol de la puce.
    public string FullName => Id is null ? Name : $"{Id} · {Name}";

    public static IReadOnlyList<FunctionalDomainOption> Build(IReadOnlyList<ModuleTile> tiles)
    {
        var options = FunctionalArchitectureCatalog.Domains
            .Select(domain =>
            {
                var domainTiles = tiles.Where(tile => tile.FunctionalDomainId == domain.Id).ToList();
                return new FunctionalDomainOption(
                    domain.Id,
                    domain.Name,
                    FunctionalArchitectureCatalog.ShortLabelFor(domain.Id),
                    domainTiles.Count,
                    domainTiles.Count(tile => tile.Status == ModuleStatus.Disponible));
            })
            .ToList();

        return
        [
            new FunctionalDomainOption(
                null,
                "Tous les domaines",
                "Tous les domaines",
                tiles.Count,
                tiles.Count(tile => tile.Status == ModuleStatus.Disponible)),
            .. options
        ];
    }
}
