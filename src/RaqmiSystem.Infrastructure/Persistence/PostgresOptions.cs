using Microsoft.Extensions.Configuration;

namespace RaqmiSystem.Infrastructure.Persistence;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string Database { get; init; } = "raqmi_system";

    public string User { get; init; } = "raqmi";

    public string Password { get; init; } = string.Empty;

    public static PostgresOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new PostgresOptions
        {
            Host = Value(section, "Host", "localhost"),
            Port = IntValue(section, "Port", 5432),
            Database = Value(section, "Database", "raqmi_system"),
            User = Value(section, "User", "raqmi"),
            Password = section["Password"]
                ?? Environment.GetEnvironmentVariable("RAQMI_POSTGRES_PASSWORD")
                ?? string.Empty
        };
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
