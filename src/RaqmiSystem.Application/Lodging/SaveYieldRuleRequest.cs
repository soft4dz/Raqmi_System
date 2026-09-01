using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Une regle de revenue management. UNE SEULE regle s'applique par nuit, celle de plus petite
/// <paramref name="Priority"/> parmi les applicables : le cumul est exclu, parce que trois regles a
/// +10 % qui se declenchent ensemble produisent +33 %, ce que personne n'a decide.
/// </summary>
public sealed record SaveYieldRuleRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    DateOnly FromDate,
    DateOnly ToDate,
    YieldTrigger Trigger,
    decimal ThresholdValue,
    decimal AdjustmentPercent,
    int Priority,
    string? RoomTypeCode = null,
    string? RatePlanCode = null,
    IReadOnlyCollection<string>? DaysOfWeek = null,
    string? Notes = null);
