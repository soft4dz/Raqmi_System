using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using Raqmi.Licensing;
using Raqmi.Shared;

namespace Raqmi.LicenseManager;

public sealed class EditorTenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = "client";
    public string Name { get; set; } = "Nouveau client";
}

public sealed class EditorLicense
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public LicenseKind Kind { get; set; } = LicenseKind.Professional;
    public LicenseMode Mode { get; set; } = LicenseMode.Offline;
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;
    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow.Date;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.Date.AddYears(1);
    public List<string> AllowedModules { get; set; } = [];
    public LicenseLimits Limits { get; set; } = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional).DefaultLimits;
    public string? ServerFingerprint { get; set; }
}

public sealed class EditorData
{
    public List<EditorTenant> Tenants { get; set; } = [];
    public List<EditorLicense> Licenses { get; set; } = [];
}

public static class EditorStore
{
    private static string DataPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Raqmi License Manager", "editor-data.json");

    private static string PrivateKeyPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Raqmi License Manager", "keys", "private.jwk.json");

    public static async Task<EditorData> LoadAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(DataPath);
            return JsonSerializer.Deserialize<EditorData>(json) ?? new EditorData();
        }
        catch
        {
            return new EditorData();
        }
    }

    public static async Task SaveAsync(EditorData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        await File.WriteAllTextAsync(DataPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task<Ed25519Jwk> EnsurePrivateKeyAsync()
    {
        if (File.Exists(PrivateKeyPath))
        {
            return JsonSerializer.Deserialize<Ed25519Jwk>(await File.ReadAllTextAsync(PrivateKeyPath))
                ?? throw new InvalidOperationException("Clé privée invalide");
        }

        var (publicKey, privateKey) = await LicenseCrypto.GenerateKeyPairAsync();
        var keysDir = Path.GetDirectoryName(PrivateKeyPath)!;
        Directory.CreateDirectory(keysDir);
        await File.WriteAllTextAsync(Path.Combine(keysDir, "public.jwk.json"), JsonSerializer.Serialize(publicKey, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(PrivateKeyPath, JsonSerializer.Serialize(privateKey, new JsonSerializerOptions { WriteIndented = true }));
        return privateKey;
    }
}
