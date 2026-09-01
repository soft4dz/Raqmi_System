using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record CancellationPolicyResponse(
    Guid Id,
    string HotelUnitCode,
    string Code,
    string Label,
    string? Description,
    bool IsActive,
    CancellationChargeBasis NoShowBasis,
    decimal NoShowValue,
    IReadOnlyCollection<CancellationPolicyRuleResponse> Rules,
    string Summary,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
