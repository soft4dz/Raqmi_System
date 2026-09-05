using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Application.Revenue;

/// <summary>
/// Synthèse d'un ensemble de recettes : compteurs par statut, les quatre montants hôteliers
/// historiques (dérivés des lignes, Other absorbant tout ce qui n'est ni hébergement, ni
/// restauration, ni bar) et, dans <see cref="Categories"/>, le total de chaque catégorie
/// présente, dans l'ordre d'affichage du paramétrage.
/// </summary>
public sealed record DailyRevenueSummaryResponse(
    DateOnly? From,
    DateOnly? To,
    string? HotelUnitCode,
    DailyRevenueStatus? Status,
    int EntryCount,
    int DraftCount,
    int SubmittedCount,
    int ValidatedCount,
    int RejectedCount,
    decimal Accommodation,
    decimal Food,
    decimal Beverage,
    decimal Other,
    decimal Total)
{
    // Propriété d'initialisation plutôt que paramètre positionnel : les producteurs qui
    // composent cette réponse ailleurs (l'accueil, par exemple) continuent de compiler sans
    // connaître les catégories, et une synthèse sans détail reste une synthèse valide.
    public IReadOnlyCollection<RevenueCategoryTotal> Categories { get; init; } = [];
}
