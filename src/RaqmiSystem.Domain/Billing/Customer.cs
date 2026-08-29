using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Billing;

public sealed class Customer : AuditableEntity
{
    private Customer()
    {
    }

    public Customer(
        string code,
        string name,
        CustomerType customerType,
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
        ApplyDetails(name, customerType, nif, rc, ai, nis, address, city, phone, email);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public CustomerType CustomerType { get; private set; } = CustomerType.Company;

    /// <summary>
    /// Numero d'identification fiscale (Algeria): exactly 15 digits when provided.
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
        CustomerType customerType,
        string? nif,
        string? rc,
        string? ai,
        string? nis,
        string? address,
        string? city,
        string? phone,
        string? email)
    {
        ApplyDetails(name, customerType, nif, rc, ai, nis, address, city, phone, email);
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
        CustomerType customerType,
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
        CustomerType = customerType;
        Nif = NormalizeNif(nif, nameof(nif));
        Rc = NormalizeOptional(rc, nameof(rc), 20);
        Ai = NormalizeOptional(ai, nameof(ai), 20);
        Nis = NormalizeOptional(nis, nameof(nis), 20);
        Address = NormalizeOptional(address, nameof(address), 200);
        City = NormalizeOptional(city, nameof(city), 80);
        Phone = NormalizeOptional(phone, nameof(phone), 40);
        Email = NormalizeEmail(email);
    }

    /// <summary>
    /// Single source of truth for the Algerian NIF rule (exactly 15 digits when provided).
    /// Exposed because the issuer of a commercial document carries a NIF too
    /// (<c>ApplicationSettings.CompanyNif</c>): the emitter and the recipient of an invoice are
    /// held to the same fiscal identifier format, so the rule must not be restated elsewhere.
    /// </summary>
    public static string? NormalizeNif(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length != 15 || !trimmed.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("NIF must be exactly 15 digits.", argumentName);
        }

        return trimmed;
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
