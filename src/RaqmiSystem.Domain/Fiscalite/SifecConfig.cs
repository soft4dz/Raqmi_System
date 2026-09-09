using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Common;

namespace RaqmiSystem.Domain.Fiscalite;

/// <summary>
/// Singleton row holding the SIFEC connector configuration, on the same pattern as
/// <see cref="RaqmiSystem.Domain.Settings.ApplicationSettings"/>: a fixed <see cref="SingletonId"/>
/// and a <see cref="SingletonKey"/> pinned by a unique index plus a check constraint, so a second
/// row is impossible. Never stores the API key itself, only a reference to where it lives in the
/// secret store - consistent with how the rest of this repository treats credentials.
/// </summary>
public sealed class SifecConfig : AuditableEntity
{
    public const string SingletonKeyValue = "GLOBAL";

    public static readonly Guid SingletonId = new("5e77e1f0-0000-4000-8000-000000000002");

    private SifecConfig()
    {
    }

    private SifecConfig(SifecMode mode, string? apiUrl, string? apiKeyReference, string? declarantNif, bool isActive)
    {
        Id = SingletonId;
        SingletonKey = SingletonKeyValue;
        Apply(mode, apiUrl, apiKeyReference, declarantNif, isActive);
    }

    public string SingletonKey { get; private set; } = SingletonKeyValue;

    public SifecMode Mode { get; private set; } = SifecMode.Sandbox;

    public string? ApiUrl { get; private set; }

    /// <summary>Reference into the secret store - never the raw API key.</summary>
    public string? ApiKeyReference { get; private set; }

    public string? DeclarantNif { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastConnectionTestAt { get; private set; }

    public bool? LastConnectionTestSucceeded { get; private set; }

    public static SifecConfig CreateDefault()
    {
        return new SifecConfig(SifecMode.Sandbox, apiUrl: null, apiKeyReference: null, declarantNif: null, isActive: false);
    }

    public void Update(SifecMode mode, string? apiUrl, string? apiKeyReference, string? declarantNif, bool isActive)
    {
        Apply(mode, apiUrl, apiKeyReference, declarantNif, isActive);
    }

    public void RecordConnectionTest(bool succeeded, DateTimeOffset now)
    {
        LastConnectionTestAt = now;
        LastConnectionTestSucceeded = succeeded;
    }

    private void Apply(SifecMode mode, string? apiUrl, string? apiKeyReference, string? declarantNif, bool isActive)
    {
        Mode = mode;
        ApiUrl = NormalizeOptional(apiUrl, 300);
        ApiKeyReference = NormalizeOptional(apiKeyReference, 200);
        DeclarantNif = Customer.NormalizeNif(declarantNif, nameof(declarantNif));
        IsActive = isActive;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }

        return trimmed;
    }
}
