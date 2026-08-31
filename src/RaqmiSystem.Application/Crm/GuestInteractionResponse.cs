using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Application.Crm;

public sealed record GuestInteractionResponse(
    Guid Id,
    string CustomerCode,
    string CustomerName,
    string? HotelUnitCode,
    DateTimeOffset OccurredAt,
    InteractionChannel Channel,
    InteractionDirection Direction,
    string Subject,
    string HandledBy,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy);
