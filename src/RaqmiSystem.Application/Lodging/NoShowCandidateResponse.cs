namespace RaqmiSystem.Application.Lodging;

using RaqmiSystem.Domain.Lodging;

public sealed record NoShowCandidateResponse(
    Guid ReservationId,
    string Number,
    string CustomerCode,
    string? CustomerName,
    string? RoomNumber,
    string RoomTypeCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    GuaranteeKind Guarantee,
    decimal TotalStayAmount,
    decimal EstimatedPenalty,
    bool Recorded);
