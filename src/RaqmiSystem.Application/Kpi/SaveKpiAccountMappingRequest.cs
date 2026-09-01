using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Rattache un prefixe de compte a un groupe de gestion. Le prefixe est une racine de code :
/// "60" attrape 60, 601 et 6011. Quand plusieurs prefixes correspondent au meme compte, le plus
/// long l'emporte, ce qui permet d'ecrire des exceptions.
/// </summary>
public sealed record SaveKpiAccountMappingRequest(
    string AccountPrefix,
    KpiAccountGroup Group,
    string Label);
