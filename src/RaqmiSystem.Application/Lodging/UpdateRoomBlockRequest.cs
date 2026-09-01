using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record UpdateRoomBlockRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string Reason,
    RoomBlockCategory Category = RoomBlockCategory.Unspecified,
    string? MaintenanceReference = null,
    string? Comment = null);
