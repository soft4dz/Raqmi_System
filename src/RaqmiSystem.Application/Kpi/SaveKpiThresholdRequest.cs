namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Fixe les bornes d'un indicateur. <paramref name="HotelUnitCode"/> nul pose la regle du
/// groupe ; renseigne, il pose une exception qui prend le pas sur elle pour cette unite.
///
/// La coherence des bornes avec le sens de lecture de l'indicateur est verifiee par le domaine,
/// pas ici : une borne favorable inferieure a la borne critique sur un indicateur ou la hausse
/// est bonne rendrait tout verdict absurde, et l'API comme le poste client doivent en etre
/// proteges de la meme facon.
/// </summary>
public sealed record SaveKpiThresholdRequest(
    string KpiCode,
    string? HotelUnitCode,
    decimal? FavorableThreshold,
    decimal? CriticalThreshold,
    decimal? TargetValue,
    string? OwnerRole,
    string? Notes);
