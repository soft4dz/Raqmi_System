namespace RaqmiSystem.Application.Mice;

public sealed record EventScheduleItemResponse(
    Guid Id,
    TimeOnly StartTime,
    string Description,
    string? Department);
