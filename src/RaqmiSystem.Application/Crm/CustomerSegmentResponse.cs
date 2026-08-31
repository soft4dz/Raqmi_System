namespace RaqmiSystem.Application.Crm;

/// <summary>
/// A segment of the customer file, with the number of guest profiles currently carrying it -
/// counted at read time, because a stored count is a number that goes stale the first time a
/// profile changes segment.
/// </summary>
public sealed record CustomerSegmentResponse(
    Guid Id,
    string Code,
    string Label,
    string? Description,
    bool IsActive,
    int GuestCount,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
