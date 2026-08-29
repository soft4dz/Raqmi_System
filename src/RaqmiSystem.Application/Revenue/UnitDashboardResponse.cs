namespace RaqmiSystem.Application.Revenue;

public sealed record UnitDashboardResponse(
    DateOnly BusinessDate,
    IReadOnlyCollection<UnitDashboardRow> Units,
    int TotalUnits,
    int UnitsWithEntry,
    int UnitsMissing,
    int UnitsPendingValidation,
    decimal GrandTotal);
