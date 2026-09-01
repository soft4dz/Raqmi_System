using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record ArrivalRowResponse(
    Guid ReservationId,
    string Number,
    string CustomerCode,
    string? CustomerName,
    string? GuestName,
    Guid? RoomId,
    string? RoomNumber,
    string RoomTypeCode,
    string? HousekeepingStatus,
    bool RoomIsReady,
    DateOnly ArrivalDate,
    DateOnly DepartureDate,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    TimeOnly? EstimatedArrivalTime,
    bool IsEarlyCheckIn,
    GuaranteeKind Guarantee,
    decimal TotalStayAmount,
    decimal DepositPaid,
    decimal Balance,
    bool IsVip,
    bool IsGroup,
    string? SpecialRequests,
    ReservationStatus Status);
