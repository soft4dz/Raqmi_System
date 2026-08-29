using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Billing;

public sealed record CustomerResponse(
    Guid Id,
    string Code,
    string Name,
    CustomerType CustomerType,
    string? Nif,
    string? Rc,
    string? Ai,
    string? Nis,
    string? Address,
    string? City,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
