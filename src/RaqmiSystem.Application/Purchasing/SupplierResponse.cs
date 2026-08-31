using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Application.Purchasing;

public sealed record SupplierResponse(
    Guid Id,
    string Code,
    string Name,
    SupplierType SupplierType,
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
