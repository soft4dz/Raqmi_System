namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Une unite hoteliere du perimetre analyse. <paramref name="IsActive"/> refletele statut
/// courant du referentiel : une unite desactivee reste presente des lors qu'elle a produit des
/// donnees dans la periode, sans quoi un chiffre d'affaires realise avant la fermeture
/// disparaitrait silencieusement d'un total groupe.
/// </summary>
public sealed record KpiUnitFact(string Code, string Name, bool IsActive);
