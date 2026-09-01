using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

/// <summary>
/// Un palier : "a partir de J-<paramref name="MinDaysBeforeArrival"/>, la penalite est celle-ci".
/// Le palier a zero jour est la clause de repli, celle qui s'applique jusqu'au jour meme.
/// </summary>
public sealed record CancellationPolicyRuleResponse(
    int MinDaysBeforeArrival,
    CancellationChargeBasis Basis,
    decimal Value);
