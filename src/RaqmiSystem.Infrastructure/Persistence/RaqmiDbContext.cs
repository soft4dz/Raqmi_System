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
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Mice;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Domain.Pos;
using RaqmiSystem.Domain.Receivables;
using RaqmiSystem.Domain.Reporting;
using RaqmiSystem.Domain.Revenue;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Domain.Sync;
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

    public DbSet<RoomTypeBed> RoomTypeBeds => Set<RoomTypeBed>();

    public DbSet<RoomBed> RoomBeds => Set<RoomBed>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Folio> Folios => Set<Folio>();

    public DbSet<FolioCharge> FolioCharges => Set<FolioCharge>();

    // ----------------------------- Module 10 : socle PMS hotelier -----------------------------
    // Ces tables portent l'inventaire et les regles de vente. Elles sont lues par la recherche de
    // disponibilite, par le garde de creation de reservation, par le forecast et - le jour ou ils
    // existeront - par le moteur de reservation directe et le channel manager. C'est deliberement
    // le MEME calcul pour tous : deux logiques d'inventaire independantes finissent toujours par
    // ne plus etre d'accord, et l'hotel survend en silence.

    /// <summary>Blocages de chambres : hors service technique (OOO) et d'exploitation (OOS).</summary>
    public DbSet<RoomBlock> RoomBlocks => Set<RoomBlock>();

    /// <summary>Regles d'exploitation par unite : heures de comptoir, ECI/LCO, inventaire.</summary>
    public DbSet<LodgingPolicy> LodgingPolicies => Set<LodgingPolicy>();

    /// <summary>Restrictions de vente : stop sell, CTA, CTD, MinLOS, MaxLOS, delais.</summary>
    public DbSet<RateRestriction> RateRestrictions => Set<RateRestriction>();

    /// <summary>Autorisations de surreservation, par type et par periode.</summary>
    public DbSet<OverbookingAllowance> OverbookingAllowances => Set<OverbookingAllowance>();

    /// <summary>Journal metier des sejours : ce qui a change, quand et par qui.</summary>
    public DbSet<ReservationEvent> ReservationEvents => Set<ReservationEvent>();

    /// <summary>Historique des chambres occupees par un sejour (changements de chambre compris).</summary>
    public DbSet<StayRoomAssignment> StayRoomAssignments => Set<StayRoomAssignment>();

    /// <summary>Referentiel des extras vendables.</summary>
    public DbSet<ExtraItem> ExtraItems => Set<ExtraItem>();

    /// <summary>Extras attaches a un sejour, a leurs conditions de vente figees.</summary>
    public DbSet<ReservationExtra> ReservationExtras => Set<ReservationExtra>();

    /// <summary>Forfaits et leur ventilation interne.</summary>
    public DbSet<Package> Packages => Set<Package>();

    public DbSet<PackageComponent> PackageComponents => Set<PackageComponent>();

    /// <summary>Acomptes attaches aux reservations.</summary>
    public DbSet<Deposit> Deposits => Set<Deposit>();

    /// <summary>Politiques d'annulation et leurs paliers.</summary>
    public DbSet<CancellationPolicy> CancellationPolicies => Set<CancellationPolicy>();

    public DbSet<CancellationPolicyRule> CancellationPolicyRules => Set<CancellationPolicyRule>();

    /// <summary>Regles de revenue management.</summary>
    public DbSet<YieldRule> YieldRules => Set<YieldRule>();

    /// <summary>Passages de night audit.</summary>
    public DbSet<NightAuditRun> NightAuditRuns => Set<NightAuditRun>();

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

    // Module 11.6 : points de vente, toujours rattaches a une unite hoteliere.
    public DbSet<PosOutlet> PosOutlets => Set<PosOutlet>();
    public DbSet<PosTable> PosTables => Set<PosTable>();
    public DbSet<PosProduct> PosProducts => Set<PosProduct>();
    public DbSet<PosTicket> PosTickets => Set<PosTicket>();
    public DbSet<PosTicketLine> PosTicketLines => Set<PosTicketLine>();

    // ---------------------- Module 10.6 : evenementiel (Groupes & MICE) ----------------------
    // Une salle de reception n'est PAS une chambre : elle se vend au creneau et non a la
    // nuitee, et n'entre ni dans la disponibilite ni dans le taux d'occupation. C'est cette
    // separation qui permet au module d'exister sans toucher au coeur reservation.
    public DbSet<FunctionSpace> FunctionSpaces => Set<FunctionSpace>();

    public DbSet<EventBooking> EventBookings => Set<EventBooking>();

    // Un allotement porte sur des CHAMBRES : LodgingService le lit a chaque recherche de
    // disponibilite pour en soustraire le solde tenu. Le module 10.6 en est proprietaire
    // fonctionnel, le module hebergement en est lecteur permanent.
    public DbSet<RoomAllotment> RoomAllotments => Set<RoomAllotment>();

    // -------------------------- Module 29 : supervision des postes --------------------------
    // Ces deux tables ne portent AUCUNE donnee metier : un inventaire des postes deployes et les
    // erreurs qu'ils signalent eux-memes. Il n'y a pas de file de synchronisation ici, et il ne
    // doit jamais y en avoir : tous les postes ecrivent deja dans cette meme base.
    public DbSet<Workstation> Workstations => Set<Workstation>();

    public DbSet<WorkstationFailure> WorkstationFailures => Set<WorkstationFailure>();

    // ------------------------------ Bibliotheque KPI ------------------------------
    // Les trois seules tables du module : bornes de pilotage, rattachement des comptes aux
    // groupes de gestion, et valeurs historisees. AUCUNE donnee d'exploitation : les
    // indicateurs sont toujours calcules sur les transactions des autres modules, jamais
    // stockes comme une seconde base metier.
    public DbSet<KpiThreshold> KpiThresholds => Set<KpiThreshold>();

    public DbSet<KpiAccountMapping> KpiAccountMappings => Set<KpiAccountMapping>();

    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();

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
