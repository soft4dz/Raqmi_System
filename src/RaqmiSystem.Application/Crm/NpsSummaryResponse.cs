namespace RaqmiSystem.Application.Crm;

/// <summary>
/// The satisfaction of a period: the three NPS families, the resulting score, and the same
/// figures unit by unit.
///
/// <paramref name="Nps"/> and <paramref name="AverageScore"/> are NULL when nobody answered - a
/// dash on the screen, not a zero that would read as a catastrophic score.
/// </summary>
public sealed record NpsSummaryResponse(
    DateOnly From,
    DateOnly To,
    string? HotelUnitCode,
    int AnswerCount,
    int Promoters,
    int Passives,
    int Detractors,
    decimal? Nps,
    decimal? AverageScore,
    IReadOnlyCollection<NpsUnitBreakdown> Units);
