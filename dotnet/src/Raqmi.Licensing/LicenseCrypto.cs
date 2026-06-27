using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace Raqmi.Licensing;

public static class LicenseCrypto
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Task<(Ed25519Jwk PublicKey, Ed25519Jwk PrivateKey)> GenerateKeyPairAsync()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "keys", "public.jwk.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "apps", "server", "keys", "public.jwk.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "keys", "public.jwk.json")),
        };

        foreach (var publicPath in candidates)
        {
            var privatePath = Path.Combine(Path.GetDirectoryName(publicPath)!, "private.jwk.json");
            if (File.Exists(publicPath) && File.Exists(privatePath))
            {
                var publicKey = JsonSerializer.Deserialize<Ed25519Jwk>(File.ReadAllText(publicPath), JsonOptions)
                    ?? throw new InvalidOperationException("Clé publique invalide");
                var privateKey = JsonSerializer.Deserialize<Ed25519Jwk>(File.ReadAllText(privatePath), JsonOptions)
                    ?? throw new InvalidOperationException("Clé privée invalide");
                return Task.FromResult((publicKey, privateKey));
            }
        }

        throw new InvalidOperationException("Aucune paire de clés trouvée. Exécutez pnpm keys:generate à la racine du dépôt.");
    }

    public static Task<string> SignPayloadAsync(object payload, Ed25519Jwk privateKey)
    {
        var jwk = ImportJwk(privateKey, includePrivate: true);
        var credentials = new SigningCredentials(jwk, "EdDSA");
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["license"] = payload },
            SigningCredentials = credentials,
            IssuedAt = DateTime.UtcNow,
        });
        return Task.FromResult(handler.WriteToken(token));
    }

    public static Task<JsonElement> VerifySignedPayloadAsync(string token, Ed25519Jwk publicKey)
    {
        var jwk = ImportJwk(publicKey, includePrivate: false);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            RequireSignedTokens = true,
            IssuerSigningKey = jwk,
        }, out var validated);

        if (validated is not System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt)
        {
            throw new InvalidOperationException("Signature de licence invalide");
        }

        var claim = jwt.Payload.TryGetValue("license", out var value) ? value : null;
        if (claim is null)
        {
            throw new InvalidOperationException("Format de licence signée invalide");
        }

        var json = JsonSerializer.Serialize(claim);
        return Task.FromResult(JsonSerializer.Deserialize<JsonElement>(json)!);
    }

    private static SecurityKey ImportJwk(Ed25519Jwk key, bool includePrivate)
    {
        var jwkJson = JsonSerializer.Serialize(key, JsonOptions);
        return new JsonWebKey(jwkJson);
    }
}

public static class LicenseFileService
{
    public const int FileVersion = 1;

    public static RaqmiLicensePayload BuildPayload(RaqmiLicensePayload input) =>
        input with { IssuedAt = input.IssuedAt == default ? DateTimeOffset.UtcNow : input.IssuedAt };

    public static async Task<RaqmiLicenseFile> SignAsync(RaqmiLicensePayload payload, Ed25519Jwk privateKey)
    {
        var unsigned = payload with { Signature = null };
        var signature = await LicenseCrypto.SignPayloadAsync(unsigned, privateKey);
        return new RaqmiLicenseFile { Version = FileVersion, Payload = unsigned, Signature = signature };
    }

    public static async Task<RaqmiLicensePayload> VerifyAsync(RaqmiLicenseFile file, Ed25519Jwk publicKey)
    {
        if (file.Version != FileVersion)
        {
            throw new InvalidOperationException($"Version de fichier licence non supportée: {file.Version}");
        }

        var verified = await LicenseCrypto.VerifySignedPayloadAsync(file.Signature, publicKey);
        var licenseId = verified.GetProperty("licenseId").GetString();
        if (licenseId != file.Payload.LicenseId)
        {
            throw new InvalidOperationException("Signature de licence invalide ou altérée");
        }

        return file.Payload with { Signature = file.Signature };
    }

    public static string Serialize(RaqmiLicenseFile file) =>
        JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public static RaqmiLicenseFile Parse(string content)
    {
        var file = JsonSerializer.Deserialize<RaqmiLicenseFile>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Fichier licence incomplet");

        if (file.Payload is null || string.IsNullOrWhiteSpace(file.Signature))
        {
            throw new InvalidOperationException("Fichier licence incomplet");
        }

        return file;
    }
}

public static class ServerFingerprint
{
    public static string Compute(string? extra = null)
    {
        var raw = string.Join('|', Environment.MachineName, Environment.OSVersion.Platform, Environment.Is64BitOperatingSystem ? "x64" : "x86", extra ?? "raqmi-v1");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}
