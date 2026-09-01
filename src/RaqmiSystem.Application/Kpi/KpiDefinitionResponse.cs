using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une fiche du catalogue, telle que l'API la publie. Elle contient tout ce qui rend un
/// indicateur discutable : sa formule, ce qu'il compte exactement, d'ou vient la donnee, ce
/// qu'il exige comme droits, et - quand il n'est pas calculable - ce qui lui manque, nomme de
/// facon actionnable.
/// </summary>
public sealed record KpiDefinitionResponse(
    string Code,
    string Name,
    string ShortName,
    KpiCategory Category,
    string Description,
    string Formula,
    KpiUnit Unit,
    KpiPolarity Polarity,
    KpiAggregation Aggregation,
    KpiScopeLevel ScopeLevel,
    KpiRefreshTrigger RefreshTriggers,
    KpiSourceModule SourceModule,
    string SourceDetail,
    IReadOnlyCollection<string> RequiredPermissions,
    KpiAvailability Availability,
    string? MissingSource,
    int FormulaVersion,
    bool Readable)
{
    /// <summary>
    /// Projette une definition du catalogue. <paramref name="readable"/> dit si le profil
    /// connecte detient toutes les permissions exigees : la fiche reste visible - connaitre la
    /// bibliotheque n'est pas connaitre les chiffres - mais l'ecran sait qu'il ne faut pas
    /// esperer de valeur.
    /// </summary>
    public static KpiDefinitionResponse From(KpiDefinition definition, bool readable)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new KpiDefinitionResponse(
            definition.Code,
            definition.Name,
            definition.ShortName,
            definition.Category,
            definition.Description,
            definition.Formula,
            definition.Unit,
            definition.Polarity,
            definition.Aggregation,
            definition.ScopeLevel,
            definition.RefreshTriggers,
            definition.SourceModule,
            definition.SourceDetail,
            definition.RequiredPermissions,
            definition.Availability,
            definition.MissingSource,
            definition.FormulaVersion,
            readable);
    }
}
