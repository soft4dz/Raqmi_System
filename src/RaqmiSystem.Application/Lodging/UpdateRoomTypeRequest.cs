namespace RaqmiSystem.Application.Lodging;

public sealed record UpdateRoomTypeRequest(
    string Label,
    int Capacity,
    string? Description = null);
