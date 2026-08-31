using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the stocks module: routing, JSON binding, the per-permission
/// authorization policies registered in Program.cs, and the module's own refusals as the client
/// really sees them (409 on an outflow that would go negative, 409 on a second validation of an
/// inventory, 400 on a malformed filter).
///
/// Each test provisions its own dedicated role carrying exactly the inventory permission keys it
/// needs. Those keys are seeded from PermissionCatalog by SecuritySeeder during factory startup,
/// so this file only turns green once "inventory.read", "inventory.write" and
/// "inventory.validate" have been added to PermissionCatalog and MapInventoryEndpoints wired in
/// Program.cs - <see cref="CreateInventoryUserAsync"/> fails with that exact message until then,
/// rather than with an opaque 403.
/// </summary>
public sealed class InventoryEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string InventoryRead = "inventory.read";
    private const string InventoryWrite = "inventory.write";
    private const string InventoryValidate = "inventory.validate";

    private static readonly DateOnly MovementDate = new(2030, 3, 15);

    private readonly RaqmiApiFactory _factory;

    public InventoryEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Stock_cycle_runs_from_referential_to_valued_stock_and_refuses_to_go_negative()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("INVHT1", "Hotel Stocks");

        await CreateInventoryUserAsync(
            "inventory.keeper",
            "inventory.keeper@example.com",
            "Magasinier",
            InventoryRead, InventoryWrite);

        var client = await _factory.CreateAuthenticatedClientAsync("inventory.keeper", Password);

        var warehouse = await CreateWarehouseAsync(client, "CYC-MAG", "Magasin cycle", unitCode);
        await CreateItemAsync(client, "CYC-FAR", "Farine T55", "kg", StockItemCategory.Alimentaire, 0m);
        await CreateItemAsync(client, "CYC-HUI", "Huile 5 L", "bidon", StockItemCategory.Alimentaire, 25m);

        // Two entries at different prices: the weighted average is (100x10 + 50x13) / 150 = 11.00.
        await PostMovementAsync(client, warehouse, "CYC-FAR", StockMovementKind.PurchaseEntry, 100m, 10.00m, "BL-1");
        await PostMovementAsync(client, warehouse, "CYC-FAR", StockMovementKind.PurchaseEntry, 50m, 13.00m, "BL-2");
        await PostMovementAsync(client, warehouse, "CYC-HUI", StockMovementKind.PurchaseEntry, 24m, 500.00m, "BL-3");

        var stock = await client.GetFromJsonAsync<WarehouseStockResponse>(
            $"/api/v1/inventory/warehouses/{warehouse}/stock",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(stock);

        var flour = stock!.Rows.Single(row => row.ItemCode == "CYC-FAR");
        Assert.Equal(150m, flour.Quantity);
        Assert.Equal(11.00m, flour.AverageUnitCost);
        Assert.Equal(1_650.00m, flour.StockValue);
        Assert.False(flour.IsBelowMinimum);

        var oil = stock.Rows.Single(row => row.ItemCode == "CYC-HUI");
        Assert.True(oil.IsBelowMinimum);

        // Total computed server-side: the screen never adds financial figures up on its own.
        Assert.Equal(13_650.00m, stock.TotalValue);

        // The alert shows up in the dedicated report too.
        var alerts = await client.GetFromJsonAsync<IReadOnlyCollection<LowStockRow>>(
            "/api/v1/inventory/low-stock",
            RaqmiApiFactory.JsonOptions);

        Assert.Contains(alerts!, alert => alert.ItemCode == "CYC-HUI" && alert.WarehouseCode == warehouse);

        // An outflow of 151 out of 150 is a conflict, not a silent negative balance.
        var overdraw = await client.PostAsJsonAsync(
            "/api/v1/inventory/movements",
            MovementRequest(warehouse, "CYC-FAR", StockMovementKind.Consumption, 151m, null, "BS-KO"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, overdraw.StatusCode);

        // ... and the registry is untouched by the refusal.
        var afterRefusal = await client.GetFromJsonAsync<WarehouseStockResponse>(
            $"/api/v1/inventory/warehouses/{warehouse}/stock",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(150m, afterRefusal!.Rows.Single(row => row.ItemCode == "CYC-FAR").Quantity);
    }

    [Fact]
    public async Task A_transfer_is_one_call_that_creates_both_halves()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("INVHT2", "Hotel Transferts");

        await CreateInventoryUserAsync(
            "inventory.mover",
            "inventory.mover@example.com",
            "Transferts",
            InventoryRead, InventoryWrite);

        var client = await _factory.CreateAuthenticatedClientAsync("inventory.mover", Password);

        var source = await CreateWarehouseAsync(client, "TRF-SRC", "Magasin source", unitCode);
        var target = await CreateWarehouseAsync(client, "TRF-DST", "Magasin destination", unitCode);
        await CreateItemAsync(client, "TRF-RIZ", "Riz", "kg", StockItemCategory.Alimentaire, 0m);

        await PostMovementAsync(client, source, "TRF-RIZ", StockMovementKind.PurchaseEntry, 40m, 120.00m, "BL-1");

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/transfers",
            new CreateStockTransferRequest(source, target, "TRF-RIZ", MovementDate, 15m, "TR-1", null, null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var transfer = await response.Content.ReadFromJsonAsync<StockTransferResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(transfer);
        Assert.Equal(transfer!.TransferGroupId, transfer.OutMovement.TransferGroupId);
        Assert.Equal(transfer.TransferGroupId, transfer.InMovement.TransferGroupId);
        Assert.Equal(-15m, transfer.OutMovement.SignedQuantity);
        Assert.Equal(15m, transfer.InMovement.SignedQuantity);

        var sourceStock = await client.GetFromJsonAsync<WarehouseStockResponse>(
            $"/api/v1/inventory/warehouses/{source}/stock",
            RaqmiApiFactory.JsonOptions);

        var targetStock = await client.GetFromJsonAsync<WarehouseStockResponse>(
            $"/api/v1/inventory/warehouses/{target}/stock",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(25m, sourceStock!.Rows.Single().Quantity);
        Assert.Equal(15m, targetStock!.Rows.Single().Quantity);

        // Capturing a single half through the movement route is refused: it would destroy goods.
        var half = await client.PostAsJsonAsync(
            "/api/v1/inventory/movements",
            MovementRequest(source, "TRF-RIZ", StockMovementKind.TransferOut, 1m, null, "TR-KO"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
    }

    [Fact]
    public async Task Validating_an_inventory_generates_the_adjustments_and_freezes_the_count()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("INVHT3", "Hotel Inventaires");

        await CreateInventoryUserAsync(
            "inventory.controller",
            "inventory.controller@example.com",
            "Controle",
            InventoryRead, InventoryWrite, InventoryValidate);

        var client = await _factory.CreateAuthenticatedClientAsync("inventory.controller", Password);

        var warehouse = await CreateWarehouseAsync(client, "INV-MAG", "Magasin inventaire", unitCode);
        await CreateItemAsync(client, "INV-SUC", "Sucre", "kg", StockItemCategory.Alimentaire, 0m);

        await PostMovementAsync(client, warehouse, "INV-SUC", StockMovementKind.PurchaseEntry, 60m, 90.00m, "BL-1");

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/inventory/counts",
            new CreateInventoryCountRequest(warehouse, MovementDate),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var count = await createResponse.Content.ReadFromJsonAsync<InventoryCountResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(count);
        Assert.Equal(InventoryCountStatus.Draft, count!.Status);
        Assert.True(count.CanEdit);

        var linesResponse = await client.PutAsJsonAsync(
            $"/api/v1/inventory/counts/{count.Id}/lines",
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest("INV-SUC", 57.5m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, linesResponse.StatusCode);

        var validateResponse = await client.PostAsync($"/api/v1/inventory/counts/{count.Id}/validate", null);

        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

        var validation = await validateResponse.Content
            .ReadFromJsonAsync<InventoryCountValidationResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(validation);
        Assert.Equal(1, validation!.AdjustmentCount);
        Assert.Equal(InventoryCountStatus.Validated, validation.Count.Status);
        Assert.False(validation.Count.CanEdit);

        // The registry now sums to what was counted on the shelf.
        var stock = await client.GetFromJsonAsync<WarehouseStockResponse>(
            $"/api/v1/inventory/warehouses/{warehouse}/stock",
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(57.5m, stock!.Rows.Single().Quantity);

        // The adjustment is a real, traceable movement of the registry.
        var movements = await client.GetFromJsonAsync<IReadOnlyCollection<StockMovementResponse>>(
            $"/api/v1/inventory/movements?warehouseCode={warehouse}&kind=InventoryAdjustment",
            RaqmiApiFactory.JsonOptions);

        var adjustment = Assert.Single(movements!);
        Assert.Equal(2.5m, adjustment.Quantity);
        Assert.False(adjustment.AdjustmentIsIncrease);

        // A validated count is immutable: neither a second validation nor a line rewrite.
        var again = await client.PostAsync($"/api/v1/inventory/counts/{count.Id}/validate", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var rewrite = await client.PutAsJsonAsync(
            $"/api/v1/inventory/counts/{count.Id}/lines",
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest("INV-SUC", 999m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, rewrite.StatusCode);
    }

    [Fact]
    public async Task Read_only_profiles_cannot_write_and_writers_cannot_validate_an_inventory()
    {
        var unitCode = await _factory.CreateHotelUnitAsync("INVHT4", "Hotel Droits");

        await CreateInventoryUserAsync(
            "inventory.reader",
            "inventory.reader@example.com",
            "Lecteur",
            InventoryRead);

        await CreateInventoryUserAsync(
            "inventory.writer",
            "inventory.writer@example.com",
            "Redacteur",
            InventoryRead, InventoryWrite);

        var reader = await _factory.CreateAuthenticatedClientAsync("inventory.reader", Password);
        var writer = await _factory.CreateAuthenticatedClientAsync("inventory.writer", Password);

        // Reading is allowed for both.
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/inventory/warehouses")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/v1/inventory/low-stock")).StatusCode);

        // Writing is not.
        var refusedWarehouse = await reader.PostAsJsonAsync(
            "/api/v1/inventory/warehouses",
            new CreateWarehouseRequest("DRT-MAG", "Magasin droits", unitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, refusedWarehouse.StatusCode);

        var refusedMovement = await reader.PostAsJsonAsync(
            "/api/v1/inventory/movements",
            MovementRequest("DRT-MAG", "DRT-ART", StockMovementKind.PurchaseEntry, 1m, 1.00m, "BL-1"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, refusedMovement.StatusCode);

        // The writer sets up an inventory but cannot close it: validating generates stock
        // adjustments, so it is a control act carrying its own permission.
        var warehouse = await CreateWarehouseAsync(writer, "DRT-MAG", "Magasin droits", unitCode);
        await CreateItemAsync(writer, "DRT-ART", "Article droits", "kg", StockItemCategory.Autre, 0m);

        var createResponse = await writer.PostAsJsonAsync(
            "/api/v1/inventory/counts",
            new CreateInventoryCountRequest(warehouse, MovementDate),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var count = await createResponse.Content.ReadFromJsonAsync<InventoryCountResponse>(RaqmiApiFactory.JsonOptions);

        await writer.PutAsJsonAsync(
            $"/api/v1/inventory/counts/{count!.Id}/lines",
            new ReplaceInventoryCountLinesRequest(new[] { new InventoryCountLineRequest("DRT-ART", 3m) }),
            RaqmiApiFactory.JsonOptions);

        var refusedValidation = await writer.PostAsync($"/api/v1/inventory/counts/{count.Id}/validate", null);

        Assert.Equal(HttpStatusCode.Forbidden, refusedValidation.StatusCode);

        // Nothing was generated by the refused validation.
        var movements = await writer.GetFromJsonAsync<IReadOnlyCollection<StockMovementResponse>>(
            $"/api/v1/inventory/movements?warehouseCode={warehouse}&kind=InventoryAdjustment",
            RaqmiApiFactory.JsonOptions);

        Assert.Empty(movements!);
    }

    [Fact]
    public async Task Anonymous_callers_are_rejected_before_any_stock_is_disclosed()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/inventory/warehouses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/inventory/items")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/inventory/movements")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/inventory/low-stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/inventory/counts")).StatusCode);
    }

    [Fact]
    public async Task Malformed_filters_and_unknown_resources_answer_before_reaching_the_registry()
    {
        await CreateInventoryUserAsync(
            "inventory.filters",
            "inventory.filters@example.com",
            "Filtres",
            InventoryRead);

        var client = await _factory.CreateAuthenticatedClientAsync("inventory.filters", Password);

        var invertedPeriod = await client.GetAsync("/api/v1/inventory/movements?from=2030-03-31&to=2030-03-01");
        Assert.Equal(HttpStatusCode.BadRequest, invertedPeriod.StatusCode);

        var unknownKind = await client.GetAsync("/api/v1/inventory/movements?kind=Teleportation");
        Assert.Equal(HttpStatusCode.BadRequest, unknownKind.StatusCode);

        var unknownStatus = await client.GetAsync("/api/v1/inventory/counts?status=Perdu");
        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);

        var unknownWarehouse = await client.GetAsync("/api/v1/inventory/warehouses/PAS-UN-MAGASIN/stock");
        Assert.Equal(HttpStatusCode.NotFound, unknownWarehouse.StatusCode);

        var unknownCount = await client.GetAsync($"/api/v1/inventory/counts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknownCount.StatusCode);
    }

    // ================================= Helpers ==================================

    private static CreateStockMovementRequest MovementRequest(
        string warehouseCode,
        string itemCode,
        StockMovementKind kind,
        decimal quantity,
        decimal? unitCost,
        string reference)
    {
        return new CreateStockMovementRequest(
            warehouseCode,
            itemCode,
            MovementDate,
            kind,
            quantity,
            unitCost,
            reference,
            LotNumber: null,
            ExpiryDate: null,
            Notes: null,
            AdjustmentIsIncrease: null);
    }

    private static async Task<string> CreateWarehouseAsync(
        HttpClient client,
        string code,
        string label,
        string hotelUnitCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/warehouses",
            new CreateWarehouseRequest(code, label, hotelUnitCode),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var warehouse = await response.Content.ReadFromJsonAsync<WarehouseResponse>(RaqmiApiFactory.JsonOptions);

        return warehouse!.Code;
    }

    private static async Task CreateItemAsync(
        HttpClient client,
        string code,
        string designation,
        string unitOfMeasure,
        StockItemCategory category,
        decimal minimumQuantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/items",
            new CreateStockItemRequest(code, designation, unitOfMeasure, category, minimumQuantity),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task PostMovementAsync(
        HttpClient client,
        string warehouseCode,
        string itemCode,
        StockMovementKind kind,
        decimal quantity,
        decimal? unitCost,
        string reference)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/movements",
            MovementRequest(warehouseCode, itemCode, kind, quantity, unitCost, reference),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Creates a user carrying exactly the requested inventory permissions, through a dedicated
    /// role. The assertion below is the honest failure mode of this whole file while the module
    /// is not wired: it names the missing keys instead of letting every test drown in 403s.
    /// </summary>
    private async Task CreateInventoryUserAsync(
        string userName,
        string email,
        string displayName,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Inventory permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.inventory.{Guid.NewGuid():N}",
            "Inventory test role",
            "Role dedicated to inventory endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
