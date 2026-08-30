using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Domain.Tariffs;
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

    public DbSet<ChartAccount> ChartAccounts => Set<ChartAccount>();

    public DbSet<AccountingJournal> AccountingJournals => Set<AccountingJournal>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();

    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<RatePlan> RatePlans => Set<RatePlan>();

    public DbSet<RatePeriod> RatePeriods => Set<RatePeriod>();

    public DbSet<CustomerConvention> CustomerConventions => Set<CustomerConvention>();

    public DbSet<RoomType> RoomTypes => Set<RoomType>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Folio> Folios => Set<Folio>();

    public DbSet<FolioCharge> FolioCharges => Set<FolioCharge>();

    /// <summary>
    /// Singleton row holding the global configuration (see
    /// <see cref="Domain.Settings.ApplicationSettings"/>). Named "Settings" rather than
    /// "ApplicationSettings" so the property name never shadows the entity type.
    /// </summary>
    public DbSet<ApplicationSettings> Settings => Set<ApplicationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("raqmi");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RaqmiDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
