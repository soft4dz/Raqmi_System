namespace RaqmiSystem.Application.Lodging;

public sealed record TapeChartRowResponse(
    Guid RoomId,
    string RoomNumber,
    string RoomTypeCode,
    string RoomTypeLabel,
    string? Floor,
    string? Building,
    bool IsActive,
    string? HousekeepingStatus,
    IReadOnlyCollection<TapeChartBarResponse> Bars);
