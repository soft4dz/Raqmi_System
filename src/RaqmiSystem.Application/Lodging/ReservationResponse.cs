using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un dossier de reservation tel que les ecrans le lisent.
///
/// <paramref name="RoomId"/> est NULLABLE : une reservation vendue par type et pas encore affectee
/// est un etat normal, pas un dossier incomplet. <paramref name="RoomTypeCode"/>, lui, est toujours
/// renseigne - c'est ce que le client a achete.
/// </summary>
public sealed record ReservationResponse(
    Guid Id,
    string HotelUnitCode,
    Guid? RoomId,
    string? RoomNumber,
    string CustomerCode,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int GuestCount,
    ReservationStatus Status,
    decimal NightlyRateSnapshot,
    string RatePlanCodeSnapshot,
    string? CancelReason,
    DateTimeOffset? CheckedInAt,
    string? CheckedInBy,
    DateTimeOffset? CheckedOutAt,
    string? CheckedOutBy,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    DateTimeOffset? NoShowAt,
    string? NoShowBy,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    string Number = "",
    string RoomTypeCode = "",
    string OriginalRoomTypeCode = "",
    int Adults = 0,
    int Children = 0,
    int Infants = 0,
    TimeOnly? EstimatedArrivalTime = null,
    TimeOnly? EstimatedDepartureTime = null,
    string? MarketSegmentCode = null,
    string? ChannelCode = null,
    string? SourceCode = null,
    string? CompanyCode = null,
    string? AgencyCode = null,
    string? ConventionCode = null,
    bool IsWalkIn = false,
    bool IsOverbooking = false,
    string? Notes = null,
    string? SpecialRequests = null,
    GuaranteeKind Guarantee = GuaranteeKind.None,
    string? GuaranteeReference = null,
    string? CancellationPolicyCode = null,
    string? CancellationPolicyDescription = null,
    decimal CancellationFeeAmount = 0m,
    decimal TotalStayAmount = 0m,
    Guid? AllotmentId = null,
    string? GuestName = null,
    string? CustomerName = null);
