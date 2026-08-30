namespace RaqmiSystem.Application.Lodging;

public sealed record FolioResponse(
    Guid Id,
    Guid ReservationId,
    decimal Balance,
    IReadOnlyCollection<FolioChargeResponse> Charges,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
