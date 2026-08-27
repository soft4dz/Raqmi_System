using Npgsql;

namespace RaqmiSystem.Infrastructure.Persistence;

public static class ConnectionStringFactory
{
    public static string Build(PostgresOptions options)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Database,
            Username = options.User,
            Password = options.Password,
            IncludeErrorDetail = false,
            Pooling = true,
            Timeout = 15,
            CommandTimeout = 30
        };

        return builder.ConnectionString;
    }
}
