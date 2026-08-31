using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Application.Purchasing;

public sealed record CreateSupplierRequest(
    string Code,
    string Name,
    SupplierType SupplierType,
    string? Nif = null,
    string? Rc = null,
    string? Ai = null,
    string? Nis = null,
    string? Address = null,
    string? City = null,
    string? Phone = null,
    string? Email = null);
