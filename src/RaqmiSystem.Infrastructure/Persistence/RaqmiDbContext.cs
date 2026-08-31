using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Audit;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Budgeting;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.HumanResources;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kitchen;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Domain.Reporting;
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

    public DbSet<RoomCondition> RoomConditions => Set<RoomCondition>();

    public DbSet<HousekeepingTask> HousekeepingTasks => Set<HousekeepingTask>();

    public DbSet<MinibarItem> MinibarItems => Set<MinibarItem>();

    public DbSet<MinibarConsumption> MinibarConsumptions => Set<MinibarConsumption>();

    public DbSet<ApprovalCircuit> ApprovalCircuits => Set<ApprovalCircuit>();

    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();

    public DbSet<ApprovalInstance> ApprovalInstances => Set<ApprovalInstance>();

    public DbSet<ApprovalInstanceStep> ApprovalInstanceSteps => Set<ApprovalInstanceStep>();

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();

    public DbSet<CustomerSegment> CustomerSegments => Set<CustomerSegment>();

    public DbSet<GuestProfile> GuestProfiles => Set<GuestProfile>();

    public DbSet<LoyaltyTier> LoyaltyTiers => Set<LoyaltyTier>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<SatisfactionEntry> SatisfactionEntries => Set<SatisfactionEntry>();

    public DbSet<GuestInteraction> GuestInteractions => Set<GuestInteraction>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmploymentContract> EmploymentContracts => Set<EmploymentContract>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<AbsenceRequest> Absences => Set<AbsenceRequest>();

    public DbSet<PayrollBonus> PayrollBonuses => Set<PayrollBonus>();

    public DbSet<Payslip> Payslips => Set<Payslip>();

    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();

    public DbSet<PayrollParameterSet> PayrollParameterSets => Set<PayrollParameterSet>();

    // ------------------------------- Vague E1 : stocks -------------------------------
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();

    public DbSet<InventoryCountLine> InventoryCountLines => Set<InventoryCountLine>();

    // ------------------------------- Vague E1 : achats -------------------------------
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    // ------------------------------ Vague E1 : cuisine -------------------------------
    public DbSet<RecipeSheet> RecipeSheets => Set<RecipeSheet>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<TemperatureCheckpoint> TemperatureCheckpoints => Set<TemperatureCheckpoint>();

    public DbSet<TemperatureReading> TemperatureReadings => Set<TemperatureReading>();

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
