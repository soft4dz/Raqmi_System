namespace Raqmi.Licensing;

public enum LicenseKind { Trial, Starter, Professional, Enterprise, Custom }
public enum LicenseMode { Online, Offline, Hybrid }
public enum LicenseStatus { Draft, Active, Suspended, Expired, Revoked }

public sealed record LicenseLimits(
    int MaxUsers,
    int MaxSites,
    int MaxStorageGb,
    int OfflineGraceDays);

public sealed record RaqmiLicensePayload
{
    public required string LicenseId { get; init; }
    public required string TenantId { get; init; }
    public required string TenantName { get; init; }
    public required LicenseKind Kind { get; init; }
    public required LicenseMode Mode { get; init; }
    public required LicenseStatus Status { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required List<string> AllowedModules { get; init; }
    public required LicenseLimits Limits { get; init; }
    public string? ServerFingerprint { get; init; }
    public string? Signature { get; init; }
}

public sealed record LicenseEvaluationContext
{
    public required DateTimeOffset Now { get; init; }
    public int UsersCount { get; init; }
    public int SitesCount { get; init; }
    public int StorageUsedGb { get; init; }
    public string? RequestedModule { get; init; }
    public DateTimeOffset? LastOnlineCheckAt { get; init; }
}

public sealed record LicenseEvaluationResult
{
    public required bool Valid { get; init; }
    public required bool ReadonlyMode { get; init; }
    public string? Reason { get; init; }
    public required List<string> AllowedModules { get; init; }
    public required List<string> BlockedModules { get; init; }
}

public sealed record RaqmiLicenseFile
{
    public int Version { get; init; } = 1;
    public required RaqmiLicensePayload Payload { get; init; }
    public required string Signature { get; init; }
}

public sealed record Ed25519Jwk
{
    public string Kty { get; init; } = "OKP";
    public string Crv { get; init; } = "Ed25519";
    public required string X { get; init; }
    public string? D { get; init; }
}
