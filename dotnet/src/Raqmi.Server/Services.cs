using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Raqmi.Licensing;
using Raqmi.Shared;

namespace Raqmi.Server;
public static class DemoData
{
    public static readonly DemoTenant Tenant = new("demo-tenant-001", "demo-hotel", "Hotel Demo Raqmi");
    public static readonly DemoUser User = new("demo-user-001", Tenant.Id, "admin@demo.raqmi.local", "Administrateur Demo", "admin", "demo1234");

    public static RaqmiLicensePayload License
    {
        get
        {
            var pack = LicensePacks.All.First(p => p.Kind == LicenseKind.Professional);
            return new RaqmiLicensePayload
            {
                LicenseId = "demo-license-001",
                TenantId = Tenant.Id,
                TenantName = Tenant.Name,
                Kind = LicenseKind.Professional,
                Mode = LicenseMode.Offline,
                Status = LicenseStatus.Active,
                IssuedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                StartsAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ExpiresAt = DateTimeOffset.Parse("2027-12-31T23:59:59Z"),
                AllowedModules = pack.Modules,
                Limits = pack.DefaultLimits,
            };
        }
    }
}

public sealed record DemoTenant(string Id, string Code, string Name);
public sealed record DemoUser(string Id, string TenantId, string Email, string FullName, string RoleCode, string Password);

public sealed class LicenseStore
{
    private RaqmiLicensePayload? _cached;
    private DateTimeOffset? _lastOnlineCheckAt;
    private readonly string _licensePath;
    private readonly string? _publicKeyPath;
    private readonly string _dataDir;

    public LicenseStore(IConfiguration config)
    {
        _licensePath = config["LICENSE_FILE_PATH"] ?? Path.Combine(AppContext.BaseDirectory, "license.license");
        _publicKeyPath = config["LICENSE_PUBLIC_KEY_PATH"];
        _dataDir = config["RAQMI_DATA_DIR"] ?? AppContext.BaseDirectory;
    }

    public string GetFingerprint() => ServerFingerprint.Compute(_dataDir);

    public RaqmiLicensePayload? GetCached() => _cached;

    public DateTimeOffset? GetLastOnlineCheckAt() => _lastOnlineCheckAt;

    public async Task<RaqmiLicensePayload?> LoadFromDiskAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;
        if (!File.Exists(_licensePath)) return null;

        var content = await File.ReadAllTextAsync(_licensePath, ct);
        var file = LicenseFileService.Parse(content);
        var publicKey = await ResolvePublicKeyAsync(ct);
        var payload = await LicenseFileService.VerifyAsync(file, publicKey);
        payload = ModuleEntitlement.NormalizePayload(payload);

        if (payload.ServerFingerprint is { } fp && fp != GetFingerprint())
        {
            throw new InvalidOperationException("Empreinte serveur incompatible avec cette licence");
        }

        _cached = payload;
        return payload;
    }

    public async Task<RaqmiLicensePayload> ImportAsync(string content, CancellationToken ct = default)
    {
        var file = LicenseFileService.Parse(content);
        var publicKey = await ResolvePublicKeyAsync(ct);
        var payload = await LicenseFileService.VerifyAsync(file, publicKey);
        payload = ModuleEntitlement.NormalizePayload(payload);

        if (payload.ServerFingerprint is { } fp && fp != GetFingerprint())
        {
            throw new InvalidOperationException("Cette licence est liée à un autre serveur");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_licensePath)!);
        await File.WriteAllTextAsync(_licensePath, LicenseFileService.Serialize(file), ct);
        _cached = payload;
        _lastOnlineCheckAt = DateTimeOffset.UtcNow;
        return payload;
    }

    private async Task<Ed25519Jwk> ResolvePublicKeyAsync(CancellationToken ct)
    {
        if (_publicKeyPath is not null && File.Exists(_publicKeyPath))
        {
            return JsonSerializer.Deserialize<Ed25519Jwk>(await File.ReadAllTextAsync(_publicKeyPath, ct))
                ?? throw new InvalidOperationException("Clé publique invalide");
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "keys", "public.jwk.json");
        if (File.Exists(bundled))
        {
            return JsonSerializer.Deserialize<Ed25519Jwk>(await File.ReadAllTextAsync(bundled, ct))
                ?? throw new InvalidOperationException("Clé publique invalide");
        }

        throw new InvalidOperationException("Clé publique éditeur non configurée sur le serveur");
    }
}

public static class JwtService
{
    public static string CreateToken(IConfiguration config, DemoUser user) =>
        CreateToken(config, user.Id, user.TenantId, user.Email, user.FullName, user.RoleCode);

    public static string CreateToken(IConfiguration config, DemoUserRecord user) =>
        CreateToken(config, user.Id, user.TenantId, user.Email, user.FullName, user.RoleCode);

    public static string CreateToken(IConfiguration config, string id, string tenantId, string email, string fullName, string roleCode)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT_SECRET"] ?? "raqmi-dev-secret-change-in-production"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id),
            new Claim("tenantId", tenantId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("fullName", fullName),
            new Claim("roleCode", roleCode),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static ClaimsPrincipal? ValidateToken(IConfiguration config, string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT_SECRET"] ?? "raqmi-dev-secret-change-in-production"));
        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = key,
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}

public static class ModuleMapper
{
    public static object ToDto(RaqmiModuleDefinition module, IEnumerable<string> allowedModules) =>
        ToDto(module, ModuleEntitlement.IsEnabled(module, allowedModules));

    public static object ToDto(RaqmiModuleDefinition module, bool enabled) => new    {
        code = RaqmiModuleCodes.ToWire(module.Code),
        label = module.Label,
        family = module.Family.ToString().ToLowerInvariant(),
        commercial = module.Commercial,
        description = module.Description,
        enabled,
    };
}
