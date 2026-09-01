namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// La date metier hoteliere d'une unite. <paramref name="IsLate"/> et
/// <paramref name="PendingDays"/> disent qu'une ou plusieurs journees attendent leur cloture :
/// le systeme n'avance pas tout seul, il le signale.
/// </summary>
public sealed record BusinessDateResponse(
    string HotelUnitCode,
    DateOnly BusinessDate,
    DateOnly CalendarDate,
    DateOnly? LastClosedDate,
    bool HasClosing,
    bool IsLate,
    int PendingDays);
