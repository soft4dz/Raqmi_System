using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;
using RaqmiSystem.Domain.Kitchen;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the kitchen module (recipe sheets and HACCP readings).
/// Each test provisions its own dedicated role carrying exactly the kitchen permission keys it
/// needs, so the per-permission authorization policies registered in Program.cs are enforced for
/// real.
///
/// NOTE INTEGRATEUR : ces tests supposent le cablage du module - MapKitchenEndpoints dans
/// Program.cs, IKitchenService -> KitchenService dans DependencyInjection.cs, et les entrees
/// "kitchen.read" / "kitchen.write" ajoutees a PermissionCatalog (que SecuritySeeder seme au
/// demarrage de la fabrique). Le helper CreateKitchenUserAsync le dit explicitement si les cles
/// manquent, plutot que d'echouer sur un 403 opaque.
/// </summary>
public sealed class KitchenEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string KitchenRead = "kitchen.read";
    private const string KitchenWrite = "kitchen.write";

    private readonly RaqmiApiFactory _factory;

    public KitchenEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The HACCP cycle end to end: a compliant reading, a breach refused without a corrective
    /// action then accepted with one, and - the point of the module - a later threshold change
    /// that leaves the recorded verdicts and their frozen thresholds untouched.
    /// </summary>
    [Fact]
    public async Task Haccp_readings_keep_the_thresholds_they_were_judged_against_when_the_checkpoint_changes()
    {
        await CreateKitchenUserAsync(
            "kitchen.chef",
            "kitchen.chef@example.com",
            "Chef de cuisine",
            KitchenRead, KitchenWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("kitchen.chef", Password);

        var checkpointResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/checkpoints",
            new CreateTemperatureCheckpointRequest("cf-haccp", "Chambre froide viandes", 0m, 4m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, checkpointResponse.StatusCode);

        var checkpoint = await checkpointResponse.Content
            .ReadFromJsonAsync<TemperatureCheckpointResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(checkpoint);
        Assert.Equal("CF-HACCP", checkpoint!.Code);

        // 1. Relevé conforme.
        var compliantResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/readings",
            new CreateTemperatureReadingRequest("CF-HACCP", 3.5m, null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, compliantResponse.StatusCode);

        var compliant = await compliantResponse.Content
            .ReadFromJsonAsync<TemperatureReadingResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(compliant);
        Assert.True(compliant!.IsCompliant);
        Assert.Equal(0m, compliant.MinTempSnapshot);
        Assert.Equal(4m, compliant.MaxTempSnapshot);

        // 2. Relevé hors plage sans action corrective : refusé.
        var refusedResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/readings",
            new CreateTemperatureReadingRequest("CF-HACCP", 11m, null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, refusedResponse.StatusCode);

        // 3. Le même relevé, motivé : accepté et tracé.
        var breachResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/readings",
            new CreateTemperatureReadingRequest("CF-HACCP", 11m, null, "Produits transférés en chambre 2"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, breachResponse.StatusCode);

        var breach = await breachResponse.Content
            .ReadFromJsonAsync<TemperatureReadingResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(breach);
        Assert.False(breach!.IsCompliant);
        Assert.Equal("Produits transférés en chambre 2", breach.CorrectiveAction);

        // 4. La plage est resserrée après coup : 3.5 ne serait plus acceptable aujourd'hui.
        var updateResponse = await client.PutAsJsonAsync(
            "/api/v1/kitchen/checkpoints/CF-HACCP",
            new UpdateTemperatureCheckpointRequest("Chambre froide viandes", 0m, 2m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        // 5. L'historique n'a pas bougé : mêmes verdicts, mêmes seuils figés.
        var history = await client.GetFromJsonAsync<TemperatureReadingResponse[]>(
            "/api/v1/kitchen/readings?checkpointCode=CF-HACCP",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(history);
        Assert.Equal(2, history!.Length);

        var storedCompliant = history.Single(reading => reading.Id == compliant.Id);
        Assert.True(storedCompliant.IsCompliant);
        Assert.Equal(0m, storedCompliant.MinTempSnapshot);
        Assert.Equal(4m, storedCompliant.MaxTempSnapshot);
        Assert.Equal("Chambre froide viandes", storedCompliant.CheckpointLabel);

        var nonCompliant = await client.GetFromJsonAsync<TemperatureReadingResponse[]>(
            "/api/v1/kitchen/readings/non-compliant?checkpointCode=CF-HACCP",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(nonCompliant);
        var listed = Assert.Single(nonCompliant!);
        Assert.Equal(breach.Id, listed.Id);
    }

    /// <summary>
    /// Recipe sheet over HTTP, with the honest cost gap: the ingredient exists in the stock module
    /// but has never entered stock, so the API flags it, excludes it from the total and warns -
    /// instead of returning a total that silently costs it at zero.
    /// </summary>
    [Fact]
    public async Task Recipe_cost_flags_and_excludes_an_ingredient_without_a_known_average_cost()
    {
        await CreateKitchenUserAsync(
            "kitchen.cost",
            "kitchen.cost@example.com",
            "Économe",
            KitchenRead, KitchenWrite);

        await CreateStockItemAsync("SEM-HTTP", "Semoule moyenne", "kg");

        using var client = await _factory.CreateAuthenticatedClientAsync("kitchen.cost", Password);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/recipes",
            new CreateRecipeRequest(
                Code: "ft-http",
                Name: "Couscous royal",
                Category: RecipeCategory.Plat,
                YieldPortions: 8,
                Allergens: "gluten",
                Instructions: "Cuire la semoule à la vapeur.",
                Ingredients: [new RecipeIngredientRequest("sem-http", 1.500m, "semoule moyenne")]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var recipe = await createResponse.Content.ReadFromJsonAsync<RecipeResponse>(RaqmiApiFactory.JsonOptions);

        Assert.NotNull(recipe);
        Assert.Equal("FT-HTTP", recipe!.Code);
        Assert.Equal(8, recipe.YieldPortions);
        Assert.Equal("gluten", recipe.Allergens);

        var ingredient = Assert.Single(recipe.Ingredients);
        Assert.Equal("SEM-HTTP", ingredient.ItemCode);
        Assert.Equal(1.500m, ingredient.Quantity);

        var cost = await client.GetFromJsonAsync<RecipeCostResponse>(
            "/api/v1/kitchen/recipes/FT-HTTP/cost",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(cost);
        Assert.True(cost!.HasMissingCosts);
        Assert.NotNull(cost.Warning);
        Assert.Contains("SEM-HTTP", cost.Warning!, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(cost.CostBasis));

        var line = Assert.Single(cost.Ingredients);
        Assert.False(line.HasCost);
        Assert.Null(line.LineCost);
        Assert.Null(line.AverageUnitCost);

        // Aucun cout connu : le total ne fabrique pas un chiffre, il reste a zero ET l'avertit.
        Assert.Equal(0m, cost.TotalCost);
        Assert.Equal(0m, cost.CostPerPortion);

        // Un ingredient inconnu du module Stocks est refuse a l'enregistrement.
        var unknownResponse = await client.PostAsJsonAsync(
            "/api/v1/kitchen/recipes",
            new CreateRecipeRequest(
                Code: "ft-http-ko",
                Name: "Fiche impossible",
                Category: RecipeCategory.Plat,
                YieldPortions: 4,
                Allergens: null,
                Instructions: null,
                Ingredients: [new RecipeIngredientRequest("ARTICLE-FANTOME", 1m, null)]),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, unknownResponse.StatusCode);
    }

    /// <summary>
    /// A read-only kitchen profile consults everything and writes nothing: the authorization
    /// policies of Program.cs are what refuse the writes, not the screen.
    /// </summary>
    [Fact]
    public async Task Read_only_profile_consults_the_kitchen_but_cannot_write_anything()
    {
        await CreateKitchenUserAsync(
            "kitchen.reader",
            "kitchen.reader@example.com",
            "Lecteur cuisine",
            KitchenRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("kitchen.reader", Password);

        var recipes = await client.GetAsync("/api/v1/kitchen/recipes");
        Assert.Equal(HttpStatusCode.OK, recipes.StatusCode);

        var checkpoints = await client.GetAsync("/api/v1/kitchen/checkpoints");
        Assert.Equal(HttpStatusCode.OK, checkpoints.StatusCode);

        var readings = await client.GetAsync("/api/v1/kitchen/readings");
        Assert.Equal(HttpStatusCode.OK, readings.StatusCode);

        var createCheckpoint = await client.PostAsJsonAsync(
            "/api/v1/kitchen/checkpoints",
            new CreateTemperatureCheckpointRequest("CF-READER", "Interdit", 0m, 4m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, createCheckpoint.StatusCode);

        var createReading = await client.PostAsJsonAsync(
            "/api/v1/kitchen/readings",
            new CreateTemperatureReadingRequest("CF-READER", 2m, null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, createReading.StatusCode);

        var createRecipe = await client.PostAsJsonAsync(
            "/api/v1/kitchen/recipes",
            new CreateRecipeRequest("FT-READER", "Interdite", RecipeCategory.Plat, 4, null, null, []),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, createRecipe.StatusCode);
    }

    [Fact]
    public async Task Recipe_category_filter_rejects_a_value_outside_the_enumeration()
    {
        await CreateKitchenUserAsync(
            "kitchen.filter",
            "kitchen.filter@example.com",
            "Filtre cuisine",
            KitchenRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("kitchen.filter", Password);

        var response = await client.GetAsync("/api/v1/kitchen/recipes?category=Entremets");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ============================== Helpers ==============================

    /// <summary>
    /// Seeds a stock item straight through the DbContext (bypassing the stock module's own
    /// endpoints), so the kitchen tests satisfy the cross-module existence check of
    /// IStockCostProvider without incidentally testing - or coupling to - the stock module's API.
    /// The item deliberately receives NO stock movement: it is exactly the "known item, no
    /// average cost yet" case the cost endpoint must flag.
    /// </summary>
    private async Task CreateStockItemAsync(string code, string designation, string unitOfMeasure)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var normalized = code.Trim().ToUpperInvariant();

        if (await dbContext.Set<StockItem>().AnyAsync(item => item.Code == normalized))
        {
            return;
        }

        var stockItem = new StockItem(code, designation, unitOfMeasure, StockItemCategory.Alimentaire);
        stockItem.MarkCreated("tests", DateTimeOffset.UtcNow);

        dbContext.Set<StockItem>().Add(stockItem);
        await dbContext.SaveChangesAsync();
    }

    private async Task CreateKitchenUserAsync(
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
            "Kitchen permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.kitchen.{Guid.NewGuid():N}",
            "Kitchen test role",
            "Role dedicated to kitchen endpoint tests.");

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
