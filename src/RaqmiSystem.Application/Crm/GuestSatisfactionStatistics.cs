namespace RaqmiSystem.Application.Crm;

/// <summary>
/// What the guest has said about the establishment. <paramref name="Nps"/> over a single guest is
/// a blunt figure - it is -100, 0 or +100 as soon as there is one answer - so the last score and
/// the average are given next to it.
/// </summary>
public sealed record GuestSatisfactionStatistics(
    int AnswerCount,
    decimal? AverageScore,
    decimal? Nps,
    DateOnly? LastSurveyDate,
    int? LastScore);
