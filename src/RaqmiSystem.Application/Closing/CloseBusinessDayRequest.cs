namespace RaqmiSystem.Application.Closing;

public sealed record CloseBusinessDayRequest(
    DateOnly BusinessDate,
    string HotelUnitCode,
    string? Notes = null);
