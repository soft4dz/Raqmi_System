using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Approvals;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Approvals;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Catalog;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Settings;
using RaqmiSystem.Domain.Treasury;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Billing;
using RaqmiSystem.Infrastructure.Catalog;
using RaqmiSystem.Infrastructure.Fiscalite;
using RaqmiSystem.Infrastructure.Inventory;
using RaqmiSystem.Infrastructure.Persistence;
using RaqmiSystem.Infrastructure.Settings;
using RaqmiSystem.Infrastructure.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le cablage REEL du module Facturation pour les tests de service : le BillingService consomme
/// le catalogue (ICatalogService) et le module Stocks (IStockOperationService), servis par leurs
/// implementations reelles sur le meme DbContext. Les harnais des modules qui ne font que
/// consommer IBillingService (MICE, CRM, allotements) passent par ici pour ne pas repeter ce
/// cablage - et pour ne pas casser a chaque collaborateur que la facturation gagne.
/// </summary>
internal static class SalesTestServices
{
    public static BillingService CreateBillingService(RaqmiDbContext dbContext, AuditLogWriter auditWriter)
    {
        var inventory = new InventoryService(dbContext, auditWriter);

        return new BillingService(
            dbContext,
            auditWriter,
            new ApplicationSettingsService(dbContext, auditWriter),
            new CatalogService(dbContext, auditWriter, inventory),
            inventory,
            new TreasuryService(dbContext, auditWriter, new AlwaysApprovedGate()),
            new VatRegisterService(dbContext, auditWriter));
    }

    /// <summary>
    /// La tresorerie ne consulte la porte d'approbation que pour les ordres de paiement, que la
    /// chaine de vente ne touche pas : une porte toujours ouverte suffit ici.
    /// </summary>
    private sealed class AlwaysApprovedGate : IApprovalGate
    {
        public Task<ApplicationResult<bool>> IsApprovedAsync(
            ApprovalSubjectType type,
            string reference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ApplicationResult<bool>.Success(true));
        }
    }
}

/// <summary>
/// Une entreprise de test qui VEND : une unite identifiee (les factures peuvent etre emises), un
/// client, un magasin, deux articles de stock, et un catalogue de trois articles vendables - deux
/// suivis en stock (COCA, EAU) et une prestation (SPA) - sur une base SQLite ":memory:" isolee.
/// </summary>
internal sealed class SalesHarness : IAsyncDisposable
{
    public const string UnitCode = "VTE1";
    public const string CustomerCode = "CLI-VTE";
    public const string WarehouseCode = "MAG-VTE";
    public const string BankAccountCode = "BNA-VTE";
    public const string CocaStockItem = "BOI-COCA";
    public const string WaterStockItem = "BOI-EAU";
    public const string CocaArticle = "COCA";
    public const string WaterArticle = "EAU";
    public const string SpaArticle = "SPA";
    public const decimal CocaPrice = 250.00m;
    public const decimal WaterPrice = 100.00m;
    public const decimal SpaPrice = 3_000.00m;

    private readonly SqliteConnection connection;

    private SalesHarness(SqliteConnection connection, RaqmiDbContext dbContext)
    {
        this.connection = connection;
        DbContext = dbContext;
        AuditWriter = new AuditLogWriter(dbContext);
        Inventory = new InventoryService(dbContext, AuditWriter);
        Catalog = new CatalogService(dbContext, AuditWriter, Inventory);
        Billing = SalesTestServices.CreateBillingService(dbContext, AuditWriter);
    }

    public RaqmiDbContext DbContext { get; }

    public AuditLogWriter AuditWriter { get; }

    public InventoryService Inventory { get; }

    public CatalogService Catalog { get; }

    public BillingService Billing { get; }

    public static OperationContext Context { get; } = new(null, "vendeur", "127.0.0.1");

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static async Task<SalesHarness> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Set<HotelUnit>().Add(new HotelUnit(UnitCode, "Boutique Vente", HotelUnitType.Hotel));
        dbContext.Set<Customer>().Add(new Customer(CustomerCode, "Client Vente", CustomerType.Individual));
        dbContext.Set<Warehouse>().Add(new Warehouse(WarehouseCode, "Magasin boutique", UnitCode));
        dbContext.Set<BankAccount>().Add(new BankAccount(BankAccountCode, "Compte courant", "BNA", "00100200300400500600"));
        dbContext.Set<StockItem>().AddRange(
            new StockItem(CocaStockItem, "Coca-Cola 33cl", "piece", StockItemCategory.Boisson),
            new StockItem(WaterStockItem, "Eau minerale 50cl", "piece", StockItemCategory.Boisson));

        // L'emetteur est identifie : sans cela l'emission est refusee (voir BillingService).
        var settings = new ApplicationSettings(
            "Hotel El Manar Spa",
            "098765432112345",
            "16/00-1234567B99",
            "16012345678",
            "543211234509876",
            "Boulevard des Martyrs",
            "Alger");
        settings.MarkCreated("tests", DateTimeOffset.UtcNow);
        dbContext.Set<ApplicationSettings>().Add(settings);

        await dbContext.SaveChangesAsync();

        dbContext.Set<Article>().AddRange(
            new Article(CocaArticle, "Coca-Cola 33cl", "piece", 19m, CocaPrice, "Boissons", tracksStock: true, stockItemCode: CocaStockItem),
            new Article(WaterArticle, "Eau minerale 50cl", "piece", 19m, WaterPrice, "Boissons", tracksStock: true, stockItemCode: WaterStockItem),
            new Article(SpaArticle, "Acces spa", "heure", 9m, SpaPrice, "Bien-etre"));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return new SalesHarness(connection, dbContext);
    }

    /// <summary>Entree d'achat dans le magasin de vente, au cout donne.</summary>
    public async Task EnterStockAsync(string itemCode, decimal quantity, decimal unitCost)
    {
        var result = await Inventory.CreateMovementAsync(
            new CreateStockMovementRequest(
                WarehouseCode,
                itemCode,
                Today,
                StockMovementKind.PurchaseEntry,
                quantity,
                unitCost,
                $"BL-{itemCode}",
                LotNumber: null,
                ExpiryDate: null,
                Notes: null,
                AdjustmentIsIncrease: null),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    /// <summary>Stock courant d'un article dans le magasin de vente : la somme du registre, comme le module.</summary>
    public async Task<decimal> StockAsync(string itemCode)
    {
        var movements = await DbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement => movement.WarehouseCode == WarehouseCode && movement.ItemCode == itemCode)
            .ToArrayAsync();

        return movements.Sum(movement => movement.SignedQuantity);
    }

    public async Task<StockMovement[]> SaleMovementsAsync()
    {
        // Tri en memoire : le fournisseur SQLite ne sait pas ordonner sur un DateTimeOffset.
        var movements = await DbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement => movement.Kind == StockMovementKind.Sale)
            .ToArrayAsync();

        return movements.OrderBy(movement => movement.CreatedAt).ToArray();
    }

    public static void AssertSucceeded<T>(ApplicationResult<T> result)
    {
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Value);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await connection.DisposeAsync();
    }
}
