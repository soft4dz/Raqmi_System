using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Application.Lodging;

public sealed record SetGuaranteeRequest(GuaranteeKind Guarantee, string? Reference = null);
