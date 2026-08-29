using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Settings;

/// <summary>
/// Server-side global configuration shared by every workstation: the identity of the
/// establishment that ISSUES the commercial documents, plus the few exploitation defaults the
/// modules read.
///
/// Singleton row. Two complementary guarantees, both of them enforced by the database rather
/// than by convention:
///   * <see cref="SingletonKey"/> is fixed to <see cref="SingletonKeyValue"/> and carries a
///     unique index plus a check constraint pinning it to that single literal - a second row is
///     therefore impossible, whatever its Id.
///   * <see cref="SingletonId"/> is a constant, well-known identifier assigned to the row at
///     construction time, so audit entries and lookups can reference a stable key that does not
///     depend on when the row was first written.
/// </summary>
public sealed class ApplicationSettings : AuditableEntity
{
    /// <summary>
    /// The only value <see cref="SingletonKey"/> may hold; combined with a unique index it caps
    /// the table at one row.
    /// </summary>
    public const string SingletonKeyValue = "GLOBAL";

    public const string DefaultCurrencyLabel = "DZD";

    public const decimal FallbackVatRate = 19m;

    public const int DefaultAuditRetentionDays = 365;

    public const int MinimumAuditRetentionDays = 30;

    public const int MaximumAuditRetentionDays = 3650;

    /// <summary>
    /// Placeholder company name used until an administrator configures the real one. Deliberately
    /// explicit rather than empty: it is legible on an issued document and makes an unconfigured
    /// installation obvious instead of producing a document with a blank emitter.
    /// </summary>
    public const string UnconfiguredCompanyName = "Etablissement non configure";

    /// <summary>
    /// Constant identifier of the singleton row.
    /// </summary>
    public static readonly Guid SingletonId = new("5e77e1f0-0000-4000-8000-000000000001");

    private ApplicationSettings()
    {
    }

    public ApplicationSettings(
        string companyName,
        string? companyNif = null,
        string? companyRc = null,
        string? companyAi = null,
        string? companyNis = null,
        string? companyAddress = null,
        string? companyCity = null,
        string? companyPhone = null,
        string? companyEmail = null,
        decimal defaultVatRate = FallbackVatRate,
        string? currencyLabel = DefaultCurrencyLabel,
        int auditRetentionDays = DefaultAuditRetentionDays)
    {
        Id = SingletonId;
        SingletonKey = SingletonKeyValue;

        ApplyCompanyIdentity(
            companyName,
            companyNif,
            companyRc,
            companyAi,
            companyNis,
            companyAddress,
            companyCity,
            companyPhone,
            companyEmail);

        ApplyOperations(defaultVatRate, currencyLabel, auditRetentionDays);
    }

    public string SingletonKey { get; private set; } = SingletonKeyValue;

    /// <summary>
    /// Legal name of the establishment: the EMITTER printed on every invoice.
    /// </summary>
    public string CompanyName { get; private set; } = string.Empty;

    /// <summary>
    /// Numero d'identification fiscale of the establishment: exactly 15 digits when provided,
    /// validated by the very same rule as a customer's NIF.
    /// </summary>
    public string? CompanyNif { get; private set; }

    public string? CompanyRc { get; private set; }

    public string? CompanyAi { get; private set; }

    public string? CompanyNis { get; private set; }

    public string? CompanyAddress { get; private set; }

    public string? CompanyCity { get; private set; }

    public string? CompanyPhone { get; private set; }

    public string? CompanyEmail { get; private set; }

    /// <summary>
    /// VAT rate proposed by default on a new invoice line. Constrained to the very same list of
    /// Algerian rates the lines themselves accept (see InvoiceLine.RequireAllowedVatRate).
    /// </summary>
    public decimal DefaultVatRate { get; private set; } = FallbackVatRate;

    public string CurrencyLabel { get; private set; } = DefaultCurrencyLabel;

    /// <summary>
    /// How long audit entries are kept, in days. Bounded to a sane window: below
    /// <see cref="MinimumAuditRetentionDays"/> the trail would be useless for a control, above
    /// <see cref="MaximumAuditRetentionDays"/> (ten years) the value is almost certainly a typo.
    /// </summary>
    public int AuditRetentionDays { get; private set; } = DefaultAuditRetentionDays;

    /// <summary>
    /// The settings an installation runs with before an administrator has configured anything.
    /// Never persisted implicitly by a read: it is what a GET returns while the row is absent,
    /// and the starting point the first update writes.
    /// </summary>
    public static ApplicationSettings CreateDefault()
    {
        return new ApplicationSettings(UnconfiguredCompanyName);
    }

    public void UpdateCompanyIdentity(
        string companyName,
        string? companyNif,
        string? companyRc,
        string? companyAi,
        string? companyNis,
        string? companyAddress,
        string? companyCity,
        string? companyPhone,
        string? companyEmail)
    {
        ApplyCompanyIdentity(
            companyName,
            companyNif,
            companyRc,
            companyAi,
            companyNis,
            companyAddress,
            companyCity,
            companyPhone,
            companyEmail);
    }

    public void UpdateOperations(decimal defaultVatRate, string? currencyLabel, int auditRetentionDays)
    {
        ApplyOperations(defaultVatRate, currencyLabel, auditRetentionDays);
    }

    private void ApplyCompanyIdentity(
        string companyName,
        string? companyNif,
        string? companyRc,
        string? companyAi,
        string? companyNis,
        string? companyAddress,
        string? companyCity,
        string? companyPhone,
        string? companyEmail)
    {
        CompanyName = RequireValue(companyName, nameof(companyName), 200);
        CompanyNif = Customer.NormalizeNif(companyNif, nameof(companyNif));
        CompanyRc = NormalizeOptional(companyRc, nameof(companyRc), 20);
        CompanyAi = NormalizeOptional(companyAi, nameof(companyAi), 20);
        CompanyNis = NormalizeOptional(companyNis, nameof(companyNis), 20);
        CompanyAddress = NormalizeOptional(companyAddress, nameof(companyAddress), 200);
        CompanyCity = NormalizeOptional(companyCity, nameof(companyCity), 80);
        CompanyPhone = NormalizeOptional(companyPhone, nameof(companyPhone), 40);
        CompanyEmail = NormalizeEmail(companyEmail);
    }

    private void ApplyOperations(decimal defaultVatRate, string? currencyLabel, int auditRetentionDays)
    {
        DefaultVatRate = InvoiceLine.RequireAllowedVatRate(defaultVatRate, nameof(defaultVatRate));
        CurrencyLabel = NormalizeOptional(currencyLabel, nameof(currencyLabel), 10) ?? DefaultCurrencyLabel;
        AuditRetentionDays = RequireRetentionDays(auditRetentionDays);
    }

    private static int RequireRetentionDays(int value)
    {
        if (value is < MinimumAuditRetentionDays or > MaximumAuditRetentionDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Audit retention must be between {MinimumAuditRetentionDays} and {MaximumAuditRetentionDays} days.");
        }

        return value;
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
