using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>Une ligne du journal metier d'un sejour.</summary>
public sealed record ReservationEventResponse(
    Guid Id,
    ReservationEventKind Kind,
    string Summary,
    DateTimeOffset OccurredAt,
    DateOnly? BusinessDate,
    string Actor,
    string? PreviousValue,
    string? NewValue);
