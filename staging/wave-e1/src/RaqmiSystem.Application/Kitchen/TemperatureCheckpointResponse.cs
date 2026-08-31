namespace RaqmiSystem.Application.Kitchen;

public sealed record TemperatureCheckpointResponse(
    Guid Id,
    string Code,
    string Label,
    decimal MinTemp,
    decimal MaxTemp,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
