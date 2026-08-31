using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record LogGuestInteractionRequest(
    string CustomerCode,
    DateTimeOffset OccurredAt,
    InteractionChannel Channel,
    InteractionDirection Direction,
    string Subject,
    string HandledBy,
    string? HotelUnitCode = null,
    string? Notes = null);
