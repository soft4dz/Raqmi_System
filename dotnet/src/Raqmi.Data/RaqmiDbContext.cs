using Microsoft.EntityFrameworkCore;
using Raqmi.Data.Entities;
using Raqmi.Data.Entities.Finance;
using Raqmi.Data.Entities.Hr;
using Raqmi.Data.Entities.Stocks;

namespace Raqmi.Data;

public class RaqmiDbContext : DbContext
{
    private readonly string? _tenantId;

    public RaqmiDbContext(DbContextOptions<RaqmiDbContext> options) : base(options) { }

    public RaqmiDbContext(DbContextOptions<RaqmiDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantId = tenantContext.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSiteAssignment> UserSiteAssignments => Set<UserSiteAssignment>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<FileObject> FileObjects => Set<FileObject>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DailyRevenueEntry> DailyRevenueEntries => Set<DailyRevenueEntry>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<TreasuryMovement> TreasuryMovements => Set<TreasuryMovement>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmploymentContract> EmploymentContracts => Set<EmploymentContract>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();
    public DbSet<InventoryLine> InventoryLines => Set<InventoryLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<TenantSettings>(e =>
        {
            e.ToTable("tenant_settings");
            e.HasKey(x => x.TenantId);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantSettings>(x => x.TenantId);
        });

        modelBuilder.Entity<Site>(e =>
        {
            e.ToTable("sites");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.HasMany(x => x.SiteAssignments).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSiteAssignment>(e =>
        {
            e.ToTable("user_site_assignments");
            e.HasKey(x => new { x.UserId, x.SiteId });
            e.HasIndex(x => x.SiteId);
        });

        modelBuilder.Entity<FileObject>(e =>
        {
            e.ToTable("file_objects");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ModuleCode, x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        modelBuilder.Entity<DailyRevenueEntry>(e => { e.ToTable("daily_revenue_entries"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<Invoice>(e => { e.ToTable("invoices"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<InvoiceLine>(e => { e.ToTable("invoice_lines"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<TreasuryMovement>(e => { e.ToTable("treasury_movements"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<Employee>(e => { e.ToTable("employees"); e.HasKey(x => x.Id); ApplyTenantFilter(e); e.HasIndex(x => new { x.TenantId, x.Matricule }).IsUnique(); });
        modelBuilder.Entity<EmploymentContract>(e => { e.ToTable("employment_contracts"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<AttendanceRecord>(e => { e.ToTable("attendance_records"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<Product>(e => { e.ToTable("products"); e.HasKey(x => x.Id); ApplyTenantFilter(e); e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); });
        modelBuilder.Entity<StockMovement>(e => { e.ToTable("stock_movements"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<InventorySession>(e => { e.ToTable("inventory_sessions"); e.HasKey(x => x.Id); ApplyTenantFilter(e); });
        modelBuilder.Entity<InventoryLine>(e => { e.ToTable("inventory_lines"); e.HasKey(x => x.Id); });

        modelBuilder.Entity<Invoice>().HasMany(x => x.Lines).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Employee>().HasMany(x => x.Contracts).WithOne(x => x.Employee).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventorySession>().HasMany(x => x.Lines).WithOne(x => x.Session).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }

    private void ApplyTenantFilter<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> builder) where T : class, ITenantEntity
    {
        if (!string.IsNullOrEmpty(_tenantId))
        {
            builder.HasQueryFilter(e => e.TenantId == _tenantId);
        }
    }
}

public interface ITenantContext
{
    string? TenantId { get; }
    string? UserId { get; }
}
