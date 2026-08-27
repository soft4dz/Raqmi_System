using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RaqmiSystem.Infrastructure.Persistence;

public sealed class DesignTimeRaqmiDbContextFactory : IDesignTimeDbContextFactory<RaqmiDbContext>
{
    public RaqmiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RAQMI_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var options = new PostgresOptions
            {
                Host = Environment.GetEnvironmentVariable("RAQMI_POSTGRES_HOST") ?? "localhost",
                Database = Environment.GetEnvironmentVariable("RAQMI_POSTGRES_DATABASE") ?? "raqmi_system",
                User = Environment.GetEnvironmentVariable("RAQMI_POSTGRES_USER") ?? "raqmi",
                Password = Environment.GetEnvironmentVariable("RAQMI_POSTGRES_PASSWORD") ?? string.Empty
            };

            connectionString = ConnectionStringFactory.Build(options);
        }

        var builder = new DbContextOptionsBuilder<RaqmiDbContext>();
        builder.UseNpgsql(connectionString);

        return new RaqmiDbContext(builder.Options);
    }
}
