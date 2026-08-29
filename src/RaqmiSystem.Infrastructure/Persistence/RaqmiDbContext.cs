using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Infrastructure.Persistence;

public sealed class RaqmiDbContext(DbContextOptions<RaqmiDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<HotelUnit> HotelUnits => Set<HotelUnit>();

    public DbSet<DailyRevenue> DailyRevenues => Set<DailyRevenue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("raqmi");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RaqmiDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
