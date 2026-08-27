namespace RaqmiSystem.Infrastructure.Persistence;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string Database { get; init; } = "raqmi_system";

    public string User { get; init; } = "raqmi";

    public string Password { get; init; } = string.Empty;
}
