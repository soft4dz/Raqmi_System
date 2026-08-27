using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RaqmiSystem.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "RaqmiSystem";

    public string Audience { get; set; } = "RaqmiSystem.Client";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public static JwtOptions FromConfiguration(
        IConfiguration configuration,
        bool allowEphemeralDevelopmentKey)
    {
        var section = configuration.GetSection(SectionName);

        var options = new JwtOptions
        {
            Issuer = Value(section, "Issuer", "RaqmiSystem"),
            Audience = Value(section, "Audience", "RaqmiSystem.Client"),
            SigningKey = section["SigningKey"]
                ?? Environment.GetEnvironmentVariable("RAQMI_JWT_SIGNING_KEY")
                ?? string.Empty,
            AccessTokenMinutes = IntValue(section, "AccessTokenMinutes", 60)
        };

        if (string.IsNullOrWhiteSpace(options.SigningKey) && allowEphemeralDevelopmentKey)
        {
            options.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        options.Validate();

        return options;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException("JWT signing key is required. Configure RAQMI_JWT__SIGNINGKEY.");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 bytes long.");
        }

        if (AccessTokenMinutes < 5)
        {
            throw new InvalidOperationException("JWT access token lifetime must be at least 5 minutes.");
        }
    }

    private static string Value(IConfiguration section, string key, string fallback)
    {
        return string.IsNullOrWhiteSpace(section[key]) ? fallback : section[key]!;
    }

    private static int IntValue(IConfiguration section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) ? value : fallback;
    }
}
