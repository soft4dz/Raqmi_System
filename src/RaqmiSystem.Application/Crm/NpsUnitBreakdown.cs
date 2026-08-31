namespace RaqmiSystem.Application.Crm;

/// <summary>The NPS of one unit over the period, so the group figure can be read for what it hides.</summary>
public sealed record NpsUnitBreakdown(
    string HotelUnitCode,
    string HotelUnitName,
    int AnswerCount,
    int Promoters,
    int Passives,
    int Detractors,
    decimal? Nps,
    decimal? AverageScore);
