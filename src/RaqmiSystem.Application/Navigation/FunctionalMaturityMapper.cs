namespace RaqmiSystem.Application.Navigation;

/// <summary>
/// Les quatre statuts du catalogue historique du client (<c>ModuleStatus</c>), redéclarés
/// ici pour que la conversion soit testable sans WPF. Les noms sont ceux du client, à
/// l'identique, pour qu'une conversion par nom ne puisse pas se tromper de valeur.
/// </summary>
public enum LegacyModuleStatus
{
    Disponible,
    ApiPrete,
    Partiel,
    Planifie
}

/// <summary>
/// Passage des statuts historiques au modèle de readiness à quatre niveaux.
/// </summary>
public static class FunctionalMaturityMapper
{
    /// <summary>
    /// Disponible → Functional ; ApiPrete et Partiel → TechnicalPreview ; Planifie → Planned.
    /// </summary>
    /// <remarks>
    /// <see cref="FunctionalMaturity.ProductionReady"/> n'est jamais attribué ici : ce niveau
    /// exige des preuves (PostgreSQL réel en CI, E2E, smoke WPF, revue) qu'aucun statut du
    /// catalogue ne sait apporter. Il ne s'obtient que par la matrice de readiness.
    /// </remarks>
    public static FunctionalMaturity FromLegacyStatus(LegacyModuleStatus status) => status switch
    {
        LegacyModuleStatus.Disponible => FunctionalMaturity.Functional,
        LegacyModuleStatus.ApiPrete => FunctionalMaturity.TechnicalPreview,
        LegacyModuleStatus.Partiel => FunctionalMaturity.TechnicalPreview,
        LegacyModuleStatus.Planifie => FunctionalMaturity.Planned,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Statut historique inconnu.")
    };

    /// <summary>Libellé français du badge de maturité.</summary>
    public static string Label(FunctionalMaturity maturity) => maturity switch
    {
        FunctionalMaturity.Planned => "Planifié",
        FunctionalMaturity.TechnicalPreview => "Aperçu technique",
        FunctionalMaturity.Functional => "Fonctionnel",
        FunctionalMaturity.ProductionReady => "Prêt pour la production",
        _ => throw new ArgumentOutOfRangeException(nameof(maturity), maturity, "Niveau de maturité inconnu.")
    };

    /// <summary>
    /// Maturité d'un conteneur : celle de son enfant le plus avancé, Planned sans enfant.
    /// </summary>
    /// <remarks>
    /// Le plus avancé et non le moins : un module dont un écran est fonctionnel EST utilisable,
    /// même si ses autres sous-modules restent planifiés. C'est la maturité du domaine, posée à
    /// la main dans le catalogue, qui dit qu'un domaine reste un aperçu technique malgré un
    /// écran livré.
    /// </remarks>
    public static FunctionalMaturity Highest(IEnumerable<FunctionalMaturity> maturities)
    {
        var highest = FunctionalMaturity.Planned;

        foreach (var maturity in maturities)
        {
            if (maturity > highest)
            {
                highest = maturity;
            }
        }

        return highest;
    }
}
