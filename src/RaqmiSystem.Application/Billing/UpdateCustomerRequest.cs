using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Application.Billing;

public sealed record UpdateCustomerRequest(
    string Name,
    CustomerType CustomerType,
    string? Nif = null,
    string? Rc = null,
    string? Ai = null,
    string? Nis = null,
    string? Address = null,
    string? City = null,
    string? Phone = null,
    string? Email = null);
