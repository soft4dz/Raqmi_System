using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Le tableau de bord de direction en une seule reponse : les indicateurs de tete, la
/// bibliotheque complete rangee par famille, les unites du groupe et les alertes en cours.
///
/// L'ecran repond a trois questions dans cet ordre : ou en sommes-nous
/// (<see cref="Headline"/>), pourquoi (<see cref="Sections"/>, avec formule et source sur
/// chaque indicateur), et ou faut-il agir (<see cref="Alerts"/> et <see cref="Units"/>).
///
/// <see cref="HiddenByPermission"/> ne cache pas ce qu'il cache : quand le profil connecte n'a
/// pas le droit de lire la paie, les indicateurs RH ne sont pas silencieusement absents, ils
/// sont comptes ici. Un tableau de bord qui perd des lignes sans le dire fait douter de tous
/// les autres chiffres.
/// </summary>
public sealed record KpiDashboardResponse(
    DateOnly From,
    DateOnly To,
    KpiPeriodGranularity Granularity,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    string? HotelUnitCode,
    KpiDsoMethod DsoMethod,
    IReadOnlyCollection<KpiMeasureResponse> Headline,
    IReadOnlyCollection<KpiCategorySection> Sections,
    IReadOnlyCollection<KpiUnitCard> Units,
    IReadOnlyCollection<KpiAlertResponse> Alerts,
    int HiddenByPermission,
    DateTimeOffset CalculatedAt,
    KpiBasis Basis);
