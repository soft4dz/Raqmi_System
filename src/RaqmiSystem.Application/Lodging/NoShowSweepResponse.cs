namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Le rapport quotidien des non-presentations : les dossiers candidats et, quand le passage a ete
/// applique, ceux reellement bascules avec leur penalite.
/// </summary>
public sealed record NoShowSweepResponse(
    string HotelUnitCode,
    DateOnly BusinessDate,
    bool Applied,
    IReadOnlyCollection<NoShowCandidateResponse> Candidates,
    int RecordedCount,
    decimal TotalPenalty);
