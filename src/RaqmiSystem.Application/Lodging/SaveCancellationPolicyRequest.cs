using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record SaveCancellationPolicyRequest(
    string HotelUnitCode,
    string Code,
    string Label,
    CancellationChargeBasis NoShowBasis,
    decimal NoShowValue,
    IReadOnlyCollection<CancellationPolicyRuleResponse> Rules,
    string? Description = null);
