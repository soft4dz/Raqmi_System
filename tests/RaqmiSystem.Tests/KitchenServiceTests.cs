using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Kitchen;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Kitchen;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Service-level coverage of the kitchen module against a dedicated SQLite ":memory:" database
/// (one per test), with the stock module pinned by a stub of its <see cref="IStockCostProvider"/>
/// contract: the kitchen only CONSUMES that contract, so these tests fix the costs it returns
/// instead of depending on inventory data.
///
/// What is pinned here: the exact material cost of a recipe, the honest handling of an
/// ingredient whose cost is unknown (flagged AND excluded from the total, never silently costed
/// at zero), the compliance verdict frozen on a reading, the mandatory corrective action, and
/// the period filtering of the HACCP log.
/// </summary>
public sealed class KitchenServiceTests
{
    private static readonly OperationContext Context = new(null, "chef", "127.0.0.1");

    // ============================== Material cost ==============================

    [Fact]
    public async Task Recipe_cost_is_quantity_times_the_current_weighted_average_cost_of_every_ingredient()
    {
        await using var harness = await HarnessAsync();

        harness.Costs.Set("SEM-01", 220.00m, "kg");
        harness.Costs.Set("AGN-01", 1_450.50m, "kg");
        harness.Costs.Set("POI-01", 96.25m, "kg");

        await CreateRecipeAsync(
            harness,
            "FT-CSC",
            yieldPortions: 8,
            ("SEM-01", 1.500m),
            ("AGN-01", 2.000m),
            ("POI-01", 0.750m));

        var result = await harness.Service.GetRecipeCostAsync("ft-csc", CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        var cost = result.Value!;

        Assert.False(cost.HasMissingCosts);
        Assert.Null(cost.Warning);
        Assert.False(string.IsNullOrWhiteSpace(cost.CostBasis));
        Assert.Equal(8, cost.YieldPortions);

        // 1.500 x 220.00 = 330.00 ; 2.000 x 1 450.50 = 2 901.00 ;
        // 0.750 x 96.25 = 72.1875, arrondi a 72.19 (2 decimales, au plus loin de zero).
        Assert.Equal(330.00m, LineOf(cost, "SEM-01").LineCost);
        Assert.Equal(2_901.00m, LineOf(cost, "AGN-01").LineCost);
        Assert.Equal(72.19m, LineOf(cost, "POI-01").LineCost);

        Assert.Equal(3_303.19m, cost.TotalCost);

        // 3 303.19 / 8 = 412.89875, arrondi a 412.90.
        Assert.Equal(412.90m, cost.CostPerPortion);

        Assert.All(cost.Ingredients, line => Assert.True(line.HasCost));
        Assert.Equal("kg", LineOf(cost, "SEM-01").UnitOfMeasure);
    }

    /// <summary>
    /// An ingredient the stock module knows but has never received (no weighted average cost yet)
    /// must be SIGNALLED and EXCLUDED from the total, never costed at zero: an honest lower bound
    /// beats a silently wrong figure.
    /// </summary>
    [Fact]
    public async Task Ingredient_without_a_known_cost_is_flagged_and_excluded_from_the_total()
    {
        await using var harness = await HarnessAsync();

        harness.Costs.Set("SEM-01", 220.00m, "kg");

        // Known item, never received: the contract answers Success with a zero average cost.
        harness.Costs.Set("AGN-01", 0m, "kg");

        await CreateRecipeAsync(
            harness,
            "FT-PART",
            yieldPortions: 2,
            ("SEM-01", 1.500m),
            ("AGN-01", 3.000m));

        var result = await harness.Service.GetRecipeCostAsync("FT-PART", CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);

        var cost = result.Value!;

        Assert.True(cost.HasMissingCosts);
        Assert.NotNull(cost.Warning);
        Assert.Contains("AGN-01", cost.Warning!, StringComparison.Ordinal);

        var missing = LineOf(cost, "AGN-01");
        Assert.False(missing.HasCost);
        Assert.Null(missing.AverageUnitCost);
        Assert.Null(missing.LineCost);

        // The costed ingredient alone makes the total: the unknown one is not counted as zero,
        // it is simply not in the sum - and the response says so.
        Assert.Equal(330.00m, cost.TotalCost);
        Assert.Equal(165.00m, cost.CostPerPortion);
    }

    [Fact]
    public async Task Recipe_cost_of_an_unknown_recipe_is_not_found()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.GetRecipeCostAsync("FT-ABSENTE", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.NotFound, result.ErrorType);
    }

    // ============================== Recipe sheets ==============================

    [Fact]
    public async Task Recipe_creation_is_refused_when_an_ingredient_is_unknown_to_the_stock_module()
    {
        await using var harness = await HarnessAsync();

        harness.Costs.Set("SEM-01", 220.00m, "kg");

        var result = await harness.Service.CreateRecipeAsync(
            new CreateRecipeRequest(
                "FT-KO",
                "Fiche",
                RecipeCategory.Plat,
                YieldPortions: 4,
                Allergens: null,
                Instructions: null,
                Ingredients:
                [
                    new RecipeIngredientRequest("SEM-01", 1m, null),
                    new RecipeIngredientRequest("INCONNU", 1m, null)
                ]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Contains("INCONNU", result.Error!, StringComparison.Ordinal);

        // A recipe pointing at a phantom item never reaches the database.
        Assert.Equal(0, await harness.DbContext.Set<RecipeSheet>().CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Recipe_creation_is_refused_when_the_yield_is_not_strictly_positive(int yieldPortions)
    {
        await using var harness = await HarnessAsync();

        harness.Costs.Set("SEM-01", 220.00m, "kg");

        var result = await harness.Service.CreateRecipeAsync(
            new CreateRecipeRequest(
                "FT-KO",
                "Fiche",
                RecipeCategory.Plat,
                yieldPortions,
                Allergens: null,
                Instructions: null,
                Ingredients: [new RecipeIngredientRequest("SEM-01", 1m, null)]),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
        Assert.Equal(0, await harness.DbContext.Set<RecipeSheet>().CountAsync());
    }

    [Fact]
    public async Task Recipe_creation_is_refused_without_any_ingredient()
    {
        await using var harness = await HarnessAsync();

        var result = await harness.Service.CreateRecipeAsync(
            new CreateRecipeRequest("FT-KO", "Fiche", RecipeCategory.Plat, 4, null, null, []),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    /// <summary>
    /// Replace-all semantics through the whole persistence stack: the lines rebuilt by the domain
    /// carry their own freshly generated Id and must be INSERTED, not mistaken for existing rows
    /// (the reason RecipeIngredient's key is configured ValueGeneratedNever).
    /// </summary>
    [Fact]
    public async Task Updating_a_recipe_replaces_its_ingredient_lines()
    {
        await using var harness = await HarnessAsync();

        harness.Costs.Set("SEM-01", 220.00m, "kg");
        harness.Costs.Set("AGN-01", 1_450.50m, "kg");
        harness.Costs.Set("POI-01", 96.25m, "kg");

        await CreateRecipeAsync(harness, "FT-MAJ", 4, ("SEM-01", 1m), ("AGN-01", 2m));

        var updated = await harness.Service.UpdateRecipeAsync(
            "FT-MAJ",
            new UpdateRecipeRequest(
                "Couscous revisité",
                RecipeCategory.Plat,
                YieldPortions: 6,
                Allergens: "gluten",
                Instructions: null,
                Ingredients:
                [
                    new RecipeIngredientRequest("SEM-01", 1.250m, "semoule fine"),
                    new RecipeIngredientRequest("POI-01", 0.500m, null)
                ]),
            Context,
            CancellationToken.None);

        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal(6, updated.Value!.YieldPortions);

        var reloaded = await harness.Service.GetRecipeAsync("FT-MAJ", CancellationToken.None);
        Assert.True(reloaded.Succeeded, reloaded.Error);

        var codes = reloaded.Value!.Ingredients
            .OrderBy(ingredient => ingredient.LineNumber)
            .Select(ingredient => ingredient.ItemCode)
            .ToArray();

        Assert.Equal(new[] { "SEM-01", "POI-01" }, codes);
        Assert.Equal(1.250m, reloaded.Value.Ingredients.Single(line => line.ItemCode == "SEM-01").Quantity);

        // The dropped line is really gone from the table, not orphaned.
        Assert.Equal(2, await harness.DbContext.Set<RecipeIngredient>().CountAsync());
    }

    // ============================== HACCP readings ==============================

    /// <summary>
    /// The rule this module exists for, end to end: editing a checkpoint's thresholds must not
    /// rewrite the verdict of the readings already recorded against the previous range.
    /// </summary>
    [Fact]
    public async Task Editing_a_checkpoint_range_never_rewrites_the_compliance_history()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);

        var recorded = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest("CF-01", 3.5m, MeasuredAt: null, CorrectiveAction: null),
            Context,
            CancellationToken.None);

        Assert.True(recorded.Succeeded, recorded.Error);
        Assert.True(recorded.Value!.IsCompliant);
        Assert.Equal(0m, recorded.Value.MinTempSnapshot);
        Assert.Equal(4m, recorded.Value.MaxTempSnapshot);

        // The range is tightened after the fact: 3.5 would no longer be acceptable today.
        var tightened = await harness.Service.UpdateCheckpointAsync(
            "CF-01",
            new UpdateTemperatureCheckpointRequest("Chambre froide", 0m, 2m),
            Context,
            CancellationToken.None);

        Assert.True(tightened.Succeeded, tightened.Error);
        Assert.Equal(2m, tightened.Value!.MaxTemp);

        var history = await harness.Service.ListReadingsAsync(
            from: null,
            to: null,
            checkpointCode: null,
            nonCompliantOnly: false,
            CancellationToken.None);

        var past = Assert.Single(history);

        // Same verdict, same frozen thresholds: the history reads exactly as it did before.
        Assert.True(past.IsCompliant);
        Assert.Equal(0m, past.MinTempSnapshot);
        Assert.Equal(4m, past.MaxTempSnapshot);

        // And the non-compliance list stays empty: nothing was retroactively turned into a breach.
        var breaches = await harness.Service.ListReadingsAsync(
            null,
            null,
            null,
            nonCompliantOnly: true,
            CancellationToken.None);

        Assert.Empty(breaches);
    }

    [Fact]
    public async Task Reading_outside_the_range_requires_a_corrective_action()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);

        var refused = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest("CF-01", 9m, null, CorrectiveAction: null),
            Context,
            CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Equal(0, await harness.DbContext.Set<TemperatureReading>().CountAsync());

        var accepted = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest("CF-01", 9m, null, "Produits transférés en chambre 2"),
            Context,
            CancellationToken.None);

        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.False(accepted.Value!.IsCompliant);
        Assert.Equal("Produits transférés en chambre 2", accepted.Value.CorrectiveAction);
    }

    [Fact]
    public async Task Reading_on_an_inactive_checkpoint_is_refused()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);

        var deactivated = await harness.Service.SetCheckpointActiveAsync(
            "CF-01",
            isActive: false,
            Context,
            CancellationToken.None);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        var result = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest("CF-01", 2m, null, null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Reading_dated_in_the_future_is_refused()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);

        var result = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest("CF-01", 2m, DateTimeOffset.UtcNow.AddHours(2), null),
            Context,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, result.ErrorType);
    }

    /// <summary>
    /// Period and non-compliance filtering of the HACCP log. Also a regression guard on the
    /// SQLite provider: a DateTimeOffset bound written with the comparison operators (or an
    /// ORDER BY pushed to the database) does not translate there and would make this test throw.
    /// </summary>
    [Fact]
    public async Task Readings_are_filtered_by_period_and_by_non_compliance_and_come_back_newest_first()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);
        await CreateCheckpointAsync(harness, "CF-02", -20m, -15m, "Congélateur CF-02");

        var now = DateTimeOffset.UtcNow;

        await RecordAsync(harness, "CF-01", 2m, now.AddDays(-10), null);
        await RecordAsync(harness, "CF-01", 9m, now.AddDays(-3), "Produits transférés");
        await RecordAsync(harness, "CF-02", -17m, now.AddDays(-1), null);

        var everything = await harness.Service.ListReadingsAsync(null, null, null, false, CancellationToken.None);

        Assert.Equal(3, everything.Count);
        Assert.True(
            everything.First().MeasuredAt >= everything.Last().MeasuredAt,
            "The HACCP log must come back most recent first.");

        var lastWeek = await harness.Service.ListReadingsAsync(
            now.AddDays(-7),
            now,
            checkpointCode: null,
            nonCompliantOnly: false,
            CancellationToken.None);

        Assert.Equal(2, lastWeek.Count);
        Assert.DoesNotContain(lastWeek, reading => reading.ValueCelsius == 2m);

        var byCheckpoint = await harness.Service.ListReadingsAsync(
            null,
            null,
            checkpointCode: "cf-02",
            nonCompliantOnly: false,
            CancellationToken.None);

        var single = Assert.Single(byCheckpoint);
        Assert.Equal("CF-02", single.CheckpointCode);
        Assert.Equal("Congélateur CF-02", single.CheckpointLabel);

        var breaches = await harness.Service.ListReadingsAsync(
            null,
            null,
            null,
            nonCompliantOnly: true,
            CancellationToken.None);

        var breach = Assert.Single(breaches);
        Assert.Equal(9m, breach.ValueCelsius);
        Assert.False(breach.IsCompliant);
    }

    [Fact]
    public async Task Checkpoint_code_is_unique()
    {
        await using var harness = await HarnessAsync();

        await CreateCheckpointAsync(harness, "CF-01", 0m, 4m);

        var duplicate = await harness.Service.CreateCheckpointAsync(
            new CreateTemperatureCheckpointRequest("cf-01", "Doublon", 1m, 5m),
            Context,
            CancellationToken.None);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, duplicate.ErrorType);
    }

    // ============================== Helpers ==============================

    private static RecipeIngredientCostResponse LineOf(RecipeCostResponse cost, string itemCode)
    {
        return cost.Ingredients.Single(line => line.ItemCode == itemCode);
    }

    private static async Task CreateRecipeAsync(
        Harness harness,
        string code,
        int yieldPortions,
        params (string ItemCode, decimal Quantity)[] ingredients)
    {
        var result = await harness.Service.CreateRecipeAsync(
            new CreateRecipeRequest(
                code,
                "Couscous royal",
                RecipeCategory.Plat,
                yieldPortions,
                Allergens: "gluten",
                Instructions: "Cuire la semoule à la vapeur.",
                Ingredients: ingredients
                    .Select(ingredient => new RecipeIngredientRequest(ingredient.ItemCode, ingredient.Quantity, null))
                    .ToArray()),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task CreateCheckpointAsync(
        Harness harness,
        string code,
        decimal minTemp,
        decimal maxTemp,
        string label = "Chambre froide")
    {
        var result = await harness.Service.CreateCheckpointAsync(
            new CreateTemperatureCheckpointRequest(code, label, minTemp, maxTemp),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task RecordAsync(
        Harness harness,
        string checkpointCode,
        decimal value,
        DateTimeOffset measuredAt,
        string? correctiveAction)
    {
        var result = await harness.Service.CreateReadingAsync(
            new CreateTemperatureReadingRequest(checkpointCode, value, measuredAt, correctiveAction),
            Context,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
    }

    private static async Task<Harness> HarnessAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = new RaqmiDbContext(
            new DbContextOptionsBuilder<RaqmiDbContext>()
                .UseSqlite(connection)
                .Options);

        await dbContext.Database.EnsureCreatedAsync();

        var costs = new StubKitchenStockCostProvider();

        return new Harness(
            connection,
            dbContext,
            costs,
            new KitchenService(dbContext, new AuditLogWriter(dbContext), costs));
    }

    private sealed class Harness(
        SqliteConnection connection,
        RaqmiDbContext dbContext,
        StubKitchenStockCostProvider costs,
        KitchenService service) : IAsyncDisposable
    {
        public RaqmiDbContext DbContext { get; } = dbContext;

        public StubKitchenStockCostProvider Costs { get; } = costs;

        public KitchenService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Deterministic stand-in for the stock module's <see cref="IStockCostProvider"/>. It honours
    /// the contract the kitchen service builds on: NotFound for an item code the stock module
    /// does not know at all, Success for a known item - with a zero average cost when that item
    /// has never entered stock. Declared inside the test class so this stub cannot collide with
    /// the stock module's own test doubles.
    /// </summary>
    private sealed class StubKitchenStockCostProvider : IStockCostProvider
    {
        private readonly Dictionary<string, ItemCost> costs = new(StringComparer.OrdinalIgnoreCase);

        public int CallCount { get; private set; }

        public void Set(string itemCode, decimal averageUnitCost, string unitOfMeasure)
        {
            var normalized = itemCode.Trim().ToUpperInvariant();
            costs[normalized] = new ItemCost(normalized, averageUnitCost, unitOfMeasure);
        }

        public Task<ApplicationResult<ItemCost>> GetAverageCostAsync(
            string itemCode,
            CancellationToken cancellationToken)
        {
            CallCount++;

            var normalized = (itemCode ?? string.Empty).Trim().ToUpperInvariant();

            return Task.FromResult(costs.TryGetValue(normalized, out var cost)
                ? ApplicationResult<ItemCost>.Success(cost)
                : ApplicationResult<ItemCost>.NotFound($"Stock item '{normalized}' was not found."));
        }
    }
}
