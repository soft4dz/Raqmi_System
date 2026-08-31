namespace RaqmiSystem.Application.Kitchen;

public sealed record CreateTemperatureCheckpointRequest(
    string Code,
    string Label,
    decimal MinTemp,
    decimal MaxTemp);
