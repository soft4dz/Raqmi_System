using RaqmiSystem.Application.Navigation;

namespace RaqmiSystem.Desktop;

public sealed record FunctionalDomainOption(
    string? Id,
    string Name,
    int ModuleCount,
    int AvailableCount)
{
    public string DisplayLabel => Id is null
        ? $"Tous les domaines · {ModuleCount} modules"
        : $"{Id} · {Name}  ({AvailableCount}/{ModuleCount})";

    public static IReadOnlyList<FunctionalDomainOption> Build(IReadOnlyList<ModuleTile> tiles)
    {
        var options = FunctionalArchitectureCatalog.Domains
            .Select(domain =>
            {
                var domainTiles = tiles.Where(tile => tile.FunctionalDomainId == domain.Id).ToList();
                return new FunctionalDomainOption(
                    domain.Id,
                    domain.Name,
                    domainTiles.Count,
                    domainTiles.Count(tile => tile.Status == ModuleStatus.Disponible));
            })
            .ToList();

        return
        [
            new FunctionalDomainOption(
                null,
                "Tous les domaines",
                tiles.Count,
                tiles.Count(tile => tile.Status == ModuleStatus.Disponible)),
            .. options
        ];
    }
}
