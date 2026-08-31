namespace RaqmiSystem.Application.Crm;

public sealed record UpdateCustomerSegmentRequest(
    string Label,
    string? Description = null);
