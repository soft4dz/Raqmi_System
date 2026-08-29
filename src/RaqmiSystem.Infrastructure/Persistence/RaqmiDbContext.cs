using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Treasury;

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

    public DbSet<DailyClosing> DailyClosings => Set<DailyClosing>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<CashReceipt> CashReceipts => Set<CashReceipt>();

    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("raqmi");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RaqmiDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
