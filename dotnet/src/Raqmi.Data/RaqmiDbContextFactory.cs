using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Raqmi.Data;

public class RaqmiDbContextFactory : IDesignTimeDbContextFactory<RaqmiDbContext>
{
    public RaqmiDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RAQMI_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=raqmi_system;Username=raqmi;Password=raqmi_password";

        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RaqmiDbContext(options);
    }
}
