using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// La chaine de vente en HTTP : l'emission d'une facture d'articles suivis en stock exige, en
/// plus d'invoices.issue, le droit d'enregistrer des mouvements de stock des qu'un magasin de
/// sortie est indique - sans quoi invoices.issue serait un chemin detourne vers le registre.
/// </summary>
public sealed class SalesEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";
    private const string UnitCode = "SALHTL";
    private const string WarehouseCode = "SAL-MAG";
    private const string StockItemCode = "SAL-COCA";

    private readonly RaqmiApiFactory _factory;

    public SalesEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Emettre_avec_un_magasin_de_sortie_exige_le_droit_de_mouvementer_le_stock()
    {
        await _factory.ConfigureApplicationSettingsAsync();
        await _factory.CreateHotelUnitAsync(UnitCode, "Sales Hotel");
        await SeedStockAsync();

        // L'emetteur "pur facturation" : il emet, mais ne touche pas au stock.
        await CreateUserAsync(
            "sales.issuer",
            PermissionCatalog.CustomersWrite,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesWrite,
            PermissionCatalog.InvoicesIssue);

        // Le vendeur complet : il emet ET mouvemente le stock.
        await CreateUserAsync(
            "sales.seller",
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesIssue,
            PermissionCatalog.InventoryWrite);

        using var issuer = await _factory.CreateAuthenticatedClientAsync("sales.issuer", Password);
        using var seller = await _factory.CreateAuthenticatedClientAsync("sales.seller", Password);

        var customer = await issuer.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest("SALCLI", "Client Vente", CustomerType.Individual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, customer.StatusCode);

        var article = await issuer.PostAsJsonAsync(
            "/api/v1/catalog/articles",
            new CreateArticleRequest("SAL-ART-COCA", "Coca-Cola 33cl", "piece", 19m, 250m, "Boissons", TracksStock: true, StockItemCode: StockItemCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, article.StatusCode);

        var created = await issuer.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                "SALCLI",
                UnitCode,
                DateOnly.FromDateTime(DateTime.UtcNow),
                new[] { new InvoiceLineRequest(null, 4m, ArticleCode: "SAL-ART-COCA") }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var draft = await created.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        var line = Assert.Single(draft!.Lines);
        Assert.Equal("SAL-ART-COCA", line.ArticleCode);
        Assert.Equal(250m, line.UnitPrice);

        // Sans magasin : le serveur refuse (ligne suivie) mais la facture reste un brouillon.
        var withoutWarehouse = await issuer.PostAsync($"/api/v1/billing/invoices/{draft.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, withoutWarehouse.StatusCode);

        // Avec magasin mais sans inventory.write : le levier est ferme.
        var forbidden = await issuer.PostAsJsonAsync(
            $"/api/v1/billing/invoices/{draft.Id}/issue",
            new IssueInvoiceRequest(WarehouseCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var issued = await seller.PostAsJsonAsync(
            $"/api/v1/billing/invoices/{draft.Id}/issue",
            new IssueInvoiceRequest(WarehouseCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);

        var invoice = await issued.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(invoice);
        Assert.Equal(InvoiceStatus.Issued, invoice!.Status);

        // 10 entrees - 4 vendues = 6, et le mouvement de vente porte le numero de la facture.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var movements = await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(movement => movement.WarehouseCode == WarehouseCode && movement.ItemCode == StockItemCode)
            .ToArrayAsync();

        Assert.Equal(6m, movements.Sum(movement => movement.SignedQuantity));
        Assert.Contains(movements, movement => movement.Kind == StockMovementKind.Sale && movement.Reference == invoice.Number);
    }

    private async Task SeedStockAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        dbContext.Set<Warehouse>().Add(new Warehouse(WarehouseCode, "Magasin vente", UnitCode));
        dbContext.Set<StockItem>().Add(new StockItem(StockItemCode, "Coca-Cola 33cl", "piece", StockItemCategory.Boisson));
        await dbContext.SaveChangesAsync();

        dbContext.Set<StockMovement>().Add(StockMovement.PurchaseEntry(
            WarehouseCode,
            StockItemCode,
            DateOnly.FromDateTime(DateTime.UtcNow),
            10m,
            80m,
            "BL-SAL-1"));

        await dbContext.SaveChangesAsync();
    }

    private async Task CreateUserAsync(string userName, params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.Equal(permissionKeys.Length, permissions.Length);

        var role = new Role($"test.sales.{Guid.NewGuid():N}", "Sales test role", "Role dedie aux tests de la chaine de vente.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, $"{userName}@example.com", userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
