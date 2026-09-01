using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Le resultat d'un passage de night audit. <paramref name="SkippedAlreadyPosted"/> non nul sur un
/// second passage est le SIGNE que l'idempotence a joue : c'est une information, pas une anomalie.
/// </summary>
public sealed record NightAuditResponse(
    Guid Id,
    string HotelUnitCode,
    DateOnly BusinessDate,
    NightAuditStatus Status,
    int PendingArrivals,
    int PendingDepartures,
    int OpenFolios,
    int RoomStateMismatches,
    int PostedRoomNights,
    int PostedExtras,
    decimal PostedAmount,
    int NoShowsRecorded,
    int SkippedAlreadyPosted,
    string? Report,
    IReadOnlyCollection<NightAuditFindingResponse> Findings,
    DateTimeOffset StartedAt,
    string StartedBy,
    DateTimeOffset? CompletedAt,
    string? CompletedBy);
