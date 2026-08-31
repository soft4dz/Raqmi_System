using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Purchasing;

/// <summary>
/// Supplier record, same template as <see cref="Customer"/>: code, name, type, Algerian fiscal
/// identifiers, contact details and an activation flag. The NIF rule is NOT restated here:
/// <see cref="Customer.NormalizeNif"/> is the single source of truth for the Algerian
/// 15-digit fiscal identifier, and a supplier's NIF obeys the very same format as a
/// customer's or the establishment's own.
/// </summary>
public sealed class Supplier : AuditableEntity
{
    private Supplier()
    {
    }

    public Supplier(
        string code,
        string name,
        SupplierType supplierType,
        string? nif = null,
        string? rc = null,
        string? ai = null,
        string? nis = null,
        string? address = null,
        string? city = null,
        string? phone = null,
        string? email = null)
    {
        Code = NormalizeCode(code);
        ApplyDetails(name, supplierType, nif, rc, ai, nis, address, city, phone, email);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public SupplierType SupplierType { get; private set; } = SupplierType.Company;

    /// <summary>
    /// Numero d'identification fiscale (Algeria): exactly 15 digits when provided
    /// (rule owned by <see cref="Customer.NormalizeNif"/>).
    /// </summary>
    public string? Nif { get; private set; }

    /// <summary>
    /// Registre du commerce number.
    /// </summary>
    public string? Rc { get; private set; }

    /// <summary>
    /// Article d'imposition number.
    /// </summary>
    public string? Ai { get; private set; }

    /// <summary>
    /// Numero d'identification statistique.
    /// </summary>
    public string? Nis { get; private set; }

    public string? Address { get; private set; }

    public string? City { get; private set; }

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public bool IsActive { get; private set; }

    public void UpdateDetails(
        string name,
        SupplierType supplierType,
        string? nif,
        string? rc,
        string? ai,
        string? nis,
        string? address,
        string? city,
        string? phone,
        string? email)
    {
        ApplyDetails(name, supplierType, nif, rc, ai, nis, address, city, phone, email);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public static string NormalizeCode(string value)
    {
        return RequireValue(value, nameof(value), 40).ToUpperInvariant();
    }

    private void ApplyDetails(
        string name,
        SupplierType supplierType,
        string? nif,
        string? rc,
        string? ai,
        string? nis,
        string? address,
        string? city,
        string? phone,
        string? email)
    {
        Name = RequireValue(name, nameof(name), 200);
        SupplierType = RequireDefinedType(supplierType);
        // The Algerian NIF format has ONE owner in this codebase (see the remark on
        // Customer.NormalizeNif): the emitter of an invoice, its recipient and a supplier are
        // all held to the same 15-digit rule, so it must not be restated here.
        Nif = Customer.NormalizeNif(nif, nameof(nif));
        Rc = NormalizeOptional(rc, nameof(rc), 20);
        Ai = NormalizeOptional(ai, nameof(ai), 20);
        Nis = NormalizeOptional(nis, nameof(nis), 20);
        Address = NormalizeOptional(address, nameof(address), 200);
        City = NormalizeOptional(city, nameof(city), 80);
        Phone = NormalizeOptional(phone, nameof(phone), 40);
        Email = NormalizeEmail(email);
    }

    private static SupplierType RequireDefinedType(SupplierType supplierType)
    {
        if (!Enum.IsDefined(supplierType))
        {
            throw new ArgumentException("Supplier type is not valid.", nameof(supplierType));
        }

        return supplierType;
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = NormalizeOptional(value, nameof(value), 160);

        if (normalized is null)
        {
            return null;
        }

        var atIndex = normalized.IndexOf('@');

        if (atIndex <= 0 || atIndex == normalized.Length - 1)
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return normalized;
    }

    private static string RequireValue(string value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", argumentName);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, string argumentName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", argumentName);
        }

        return trimmed;
    }
}
