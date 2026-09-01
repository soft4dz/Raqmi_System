namespace RaqmiSystem.Application.Lodging;

/// <summary>Une chambre reellement occupee par un sejour, sur une plage de temps.</summary>
public sealed record StayRoomAssignmentResponse(
    Guid Id,
    Guid RoomId,
    string RoomNumber,
    string RoomTypeCode,
    DateTimeOffset AssignedAt,
    string AssignedBy,
    DateTimeOffset? ReleasedAt,
    string? ReleasedBy,
    string? Reason,
    bool IsCurrent);
