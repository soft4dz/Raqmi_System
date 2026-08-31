using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Purchasing;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage of the purchasing module. Each test provisions its own
/// dedicated role carrying exactly the purchasing permission keys it needs (the keys are seeded
/// from PermissionCatalog by SecuritySeeder during factory startup), so the per-permission
/// authorization policies registered in Program.cs are enforced for real - which is the point
/// here: purchasing.approve and purchasing.receive are DISTINCT from purchasing.write, and
/// nothing but a real 403 proves it.
///
/// The purchasing module CONSUMES the stock module's <see cref="IStockOperationService"/> and
/// <see cref="IStockCostProvider"/>; these tests pin both by swapping those two registrations
/// for the deterministic stubs in a derived factory (same shared SQLite database), so they
/// exercise the purchasing workflows without depending on stock data.
///
/// Purchase order numbers follow the APPROVAL year (UtcNow at approval time), not the
/// backdatable order date, so every test in the class shares the current year's sequence: the
/// assertions are written to be independent of the order xunit runs them in (prefix checks, no
/// absolute sequence numbers).
/// </summary>
public sealed class PurchasingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string PurchasingRead = "purchasing.read";
    private const string PurchasingWrite = "purchasing.write";
    private const string PurchasingApprove = "purchasing.approve";
    private const string PurchasingReceive = "purchasing.receive";

    private const string SugarCode = "SUC-01";
    private const string OilCode = "HUI-02";

    private readonly RaqmiApiFactory _factory;

    public PurchasingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Purchase_order_runs_the_full_draft_approve_receive_cycle_across_three_distinct_rights()
    {
        await CreatePurchasingUserAsync(
            "purchasing.buyer",
            "purchasing.buyer@example.com",
            "Purchasing Buyer",
            PurchasingRead, PurchasingWrite);

        await CreatePurchasingUserAsync(
            "purchasing.approver",
            "purchasing.approver@example.com",
            "Purchasing Approver",
            PurchasingRead, PurchasingApprove);

        await CreatePurchasingUserAsync(
            "purchasing.receiver",
            "purchasing.receiver@example.com",
            "Purchasing Receiver",
            PurchasingRead, PurchasingReceive);

        var purchasingFactory = CreatePurchasingFactory();

        using var buyerClient = await LoginAsync(purchasingFactory, "purchasing.buyer");
        using var approverClient = await LoginAsync(purchasingFactory, "purchasing.approver");
        using var receiverClient = await LoginAsync(purchasingFactory, "purchasing.receiver");

        var supplierResponse = await buyerClient.PostAsJsonAsync(
            "/api/v1/purchasing/suppliers",
            new CreateSupplierRequest(
                Code: "frn-medina",
                Name: "SARL Medina Distribution",
                SupplierType: SupplierType.Company,
                Nif: "098765432112345",
                City: "Alger"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);

        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(supplier);
        Assert.Equal("FRN-MEDINA", supplier!.Code);

        var createResponse = await buyerClient.PostAsJsonAsync(
            "/api/v1/purchasing/orders",
            new CreatePurchaseOrderRequest(
                SupplierCode: "FRN-MEDINA",
                WarehouseCode: "dep-central",
                OrderDate: new DateOnly(2026, 4, 12),
                Lines:
                [
                    new PurchaseOrderLineRequest(SugarCode, "Sucre cristallise 50 kg", 20m, 4_500.00m),
                    new PurchaseOrderLineRequest(OilCode, "Huile de table 5 L", 12m, 1_250.50m)
                ]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        // A draft carries no number: an abandoned draft never burns a slot in the sequence.
        Assert.Null(draft!.Number);
        Assert.Equal(PurchaseOrderStatus.Draft, draft.Status);
        Assert.Equal("DEP-CENTRAL", draft.WarehouseCode);
        Assert.Equal(105_006.00m, draft.TotalExclVat);

        // purchasing.write does NOT grant approval: engaging the expense is another right.
        var forbiddenApproval = await buyerClient.PostAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApproval.StatusCode);

        var approveResponse = await approverClient.PostAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/approve", content: null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var approved = await approveResponse.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal(PurchaseOrderStatus.Approved, approved!.Status);
        Assert.NotNull(approved.Number);
        Assert.StartsWith($"BC-{DateTimeOffset.UtcNow.Year}-", approved.Number);
        Assert.Equal("purchasing.approver", approved.ApprovedBy);

        // Approval froze the lines: rewriting them is now a state conflict.
        var frozenLines = await buyerClient.PutAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/lines",
            new UpdatePurchaseOrderLinesRequest([new PurchaseOrderLineRequest(SugarCode, "Sucre", 999m, 1.00m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, frozenLines.StatusCode);

        var sugarLineId = approved.Lines.Single(line => line.ItemCode == SugarCode).Id;
        var oilLineId = approved.Lines.Single(line => line.ItemCode == OilCode).Id;

        // Neither write nor approve grants the warehouse gesture: receiving is a third right.
        var forbiddenReceipt = await approverClient.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(sugarLineId, 8m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReceipt.StatusCode);

        var firstReceipt = await receiverClient.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/receive",
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugarLineId, 8m, "LOT-2026-04", new DateOnly(2027, 4, 30))
            ]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, firstReceipt.StatusCode);

        var partiallyReceived = await firstReceipt.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(partiallyReceived);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, partiallyReceived!.Status);
        Assert.Equal(8m, partiallyReceived.TotalQuantityReceived);
        Assert.Equal(32m, partiallyReceived.TotalQuantityOrdered);
        Assert.Equal(12m, partiallyReceived.Lines.Single(line => line.ItemCode == SugarCode).RemainingQuantity);

        // Receiving more than what remains is refused, whole delivery included.
        var overReceipt = await receiverClient.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/receive",
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugarLineId, 13m),
                new ReceivePurchaseOrderLineRequest(oilLineId, 2m)
            ]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, overReceipt.StatusCode);

        // Nothing of that refused delivery landed: the oil line is still untouched.
        var afterRefusal = await ReadOrderAsync(receiverClient, draft.Id);
        Assert.Equal(8m, afterRefusal.TotalQuantityReceived);
        Assert.Equal(0m, afterRefusal.Lines.Single(line => line.ItemCode == OilCode).QuantityReceived);

        // A delivery has landed: the order is now the supporting document of real stock
        // movements and can no longer be voided.
        var forbiddenCancel = await buyerClient.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/cancel",
            new CancelPurchaseOrderRequest("Fournisseur defaillant"),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, forbiddenCancel.StatusCode);

        var finalReceipt = await receiverClient.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/receive",
            new ReceivePurchaseOrderRequest(
            [
                new ReceivePurchaseOrderLineRequest(sugarLineId, 12m),
                new ReceivePurchaseOrderLineRequest(oilLineId, 12m)
            ]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, finalReceipt.StatusCode);

        var received = await finalReceipt.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(received);
        Assert.Equal(PurchaseOrderStatus.Received, received!.Status);
        Assert.Equal(32m, received.TotalQuantityReceived);
        Assert.False(received.CanReceive);

        // The stock module was asked for one entry per delivery, referenced by the order
        // number and valued at the ORDERED unit price.
        var requests = purchasingFactory.Services.GetRequiredService<PurchasingStockOperationStub>().Requests;
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("DEP-CENTRAL", request.WarehouseCode));
        Assert.All(requests, request => Assert.Equal(received.Number, request.Reference));
        Assert.Equal(4_500.00m, requests[0].Lines.Single(line => line.ItemCode == SugarCode).UnitCost);
        Assert.Equal(1_250.50m, requests[1].Lines.Single(line => line.ItemCode == OilCode).UnitCost);
    }

    [Fact]
    public async Task A_draft_can_be_cancelled_with_a_reason_before_any_delivery()
    {
        await CreatePurchasingUserAsync(
            "purchasing.canceller",
            "purchasing.canceller@example.com",
            "Purchasing Canceller",
            PurchasingRead, PurchasingWrite);

        var purchasingFactory = CreatePurchasingFactory();

        using var client = await LoginAsync(purchasingFactory, "purchasing.canceller");

        var supplierResponse = await client.PostAsJsonAsync(
            "/api/v1/purchasing/suppliers",
            new CreateSupplierRequest("FRN-ANNUL", "Fournisseur annulation", SupplierType.Company),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, supplierResponse.StatusCode);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/purchasing/orders",
            new CreatePurchaseOrderRequest(
                "FRN-ANNUL",
                "DEP-CENTRAL",
                new DateOnly(2026, 5, 4),
                [new PurchaseOrderLineRequest(SugarCode, "Sucre cristallise 50 kg", 3m, 4_500.00m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        // The reason is mandatory: an unmotivated cancellation is a bad request, not a silent
        // void of an engagement.
        var missingReason = await client.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft!.Id}/cancel",
            new CancelPurchaseOrderRequest("   "),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{draft.Id}/cancel",
            new CancelPurchaseOrderRequest("Budget reporte au trimestre suivant"),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(cancelled);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled!.Status);
        Assert.Equal("Budget reporte au trimestre suivant", cancelled.CancellationReason);
        Assert.Null(cancelled.Number);
    }

    [Fact]
    public async Task A_reader_consults_but_writes_nothing_and_bad_filters_are_rejected()
    {
        await CreatePurchasingUserAsync(
            "purchasing.reader",
            "purchasing.reader@example.com",
            "Purchasing Reader",
            PurchasingRead);

        var purchasingFactory = CreatePurchasingFactory();

        using var client = await LoginAsync(purchasingFactory, "purchasing.reader");

        var listSuppliers = await client.GetAsync("/api/v1/purchasing/suppliers");
        Assert.Equal(HttpStatusCode.OK, listSuppliers.StatusCode);

        var listOrders = await client.GetAsync("/api/v1/purchasing/orders?status=Approved");
        Assert.Equal(HttpStatusCode.OK, listOrders.StatusCode);

        // Query parameters are validated before the service is reached.
        var invertedRange = await client.GetAsync("/api/v1/purchasing/orders?from=2026-05-01&to=2026-04-01");
        Assert.Equal(HttpStatusCode.BadRequest, invertedRange.StatusCode);

        var unknownStatus = await client.GetAsync("/api/v1/purchasing/orders?status=Livree");
        Assert.Equal(HttpStatusCode.BadRequest, unknownStatus.StatusCode);

        // Read grants nothing else: neither the referential, nor the orders, nor the receipts.
        var forbiddenSupplier = await client.PostAsJsonAsync(
            "/api/v1/purchasing/suppliers",
            new CreateSupplierRequest("FRN-LECT", "Fournisseur lecteur", SupplierType.Company),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenSupplier.StatusCode);

        var forbiddenOrder = await client.PostAsJsonAsync(
            "/api/v1/purchasing/orders",
            new CreatePurchaseOrderRequest(
                "FRN-MEDINA",
                "DEP-CENTRAL",
                new DateOnly(2026, 4, 12),
                [new PurchaseOrderLineRequest(SugarCode, "Sucre", 1m, 10.00m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenOrder.StatusCode);

        var forbiddenApproval = await client.PostAsync(
            $"/api/v1/purchasing/orders/{Guid.NewGuid()}/approve", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApproval.StatusCode);

        var forbiddenReceipt = await client.PostAsJsonAsync(
            $"/api/v1/purchasing/orders/{Guid.NewGuid()}/receive",
            new ReceivePurchaseOrderRequest([new ReceivePurchaseOrderLineRequest(Guid.NewGuid(), 1m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenReceipt.StatusCode);
    }

    private static async Task<PurchaseOrderResponse> ReadOrderAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/purchasing/orders/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<PurchaseOrderResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(order);

        return order!;
    }

    /// <summary>
    /// Derived factory sharing the fixture's SQLite database, with the two stock contracts
    /// swapped for the deterministic stubs (the purchasing module only consumes them). The
    /// operation stub is registered as itself as well, so a test can read back exactly what
    /// purchasing asked the stock module for.
    /// </summary>
    private WebApplicationFactory<Program> CreatePurchasingFactory()
    {
        var stockOperations = new PurchasingStockOperationStub();
        var stockCosts = new PurchasingStockCostStub(SugarCode, OilCode);

        return _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStockOperationService>();
            services.RemoveAll<IStockCostProvider>();

            services.AddSingleton(stockOperations);
            services.AddSingleton<IStockOperationService>(stockOperations);
            services.AddSingleton<IStockCostProvider>(stockCosts);
        }));
    }

    /// <summary>
    /// Logs in through the real HTTP endpoint of the DERIVED factory and returns a client with
    /// the Bearer token set (mirrors RaqmiApiFactory.CreateAuthenticatedClientAsync, which is
    /// not reachable from a WithWebHostBuilder-derived factory).
    /// </summary>
    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string userName)
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userName, Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given
    /// purchasing permission keys. The permissions themselves must already exist (SecuritySeeder
    /// seeds every PermissionCatalog entry during factory initialization) - the assertion below
    /// fails fast with a clear signal if the purchasing keys have not been added to
    /// PermissionCatalog yet.
    /// </summary>
    private async Task CreatePurchasingUserAsync(
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
            "Purchasing permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.purchasing.{Guid.NewGuid():N}",
            "Purchasing test role",
            "Role dedicated to purchasing endpoint tests.");

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
