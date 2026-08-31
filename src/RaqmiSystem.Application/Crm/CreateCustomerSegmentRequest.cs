namespace RaqmiSystem.Application.Crm;

public sealed record CreateCustomerSegmentRequest(
    string Code,
    string Label,
    string? Description = null);
