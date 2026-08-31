namespace RaqmiSystem.Application.Kitchen;

public sealed record UpdateTemperatureCheckpointRequest(
    string Label,
    decimal MinTemp,
    decimal MaxTemp);
