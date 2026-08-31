using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Kitchen;
using RaqmiSystem.Infrastructure.Persistence;
using System.Text.Json;

namespace RaqmiSystem.Infrastructure.Kitchen;

/// <summary>
/// Kitchen, production &amp; quality module. The stock module is consumed EXCLUSIVELY through
/// the <see cref="IStockCostProvider"/> application contract (owned and implemented by the
/// stock module): this service never touches the inventory tables directly, so the two
/// modules stay decoupled at the database level.
///
/// Contract assumption, shared with the stock module: <c>GetAverageCostAsync</c> returns
/// NotFound for an item code that does not exist at all, and Success for an existing item -
/// with <c>AverageUnitCost</c> zero (or negative, defensively) when the item never entered
/// stock and therefore has no known cost yet. Recipe-save validation and cost computation
/// below both build on that reading.
/// </summary>
public sealed class KitchenService(
    RaqmiDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    IStockCostProvider stockCostProvider) : IKitchenService
{
    private const string RecipesEntity = "kitchen.recipe_sheets";

    private const string CheckpointsEntity = "kitchen.temperature_checkpoints";

    private const string ReadingsEntity = "kitchen.temperature_readings";

    /// <summary>
    /// Caveat carried verbatim by every cost response: the figures are the CURRENT weighted
    /// average costs (PMP) at computation time, not historical costs - the same recipe costed
    /// tomorrow can give a different figure after new stock receipts.
    /// </summary>
    private const string CostBasisNote =
        "Couts calcules sur les couts moyens ponderes (PMP) COURANTS du stock au moment du calcul, " +
        "pas sur des couts historiques.";

    // ============================== Recipes ==============================

    public async Task<IReadOnlyCollection<RecipeResponse>> ListRecipesAsync(
        string? search,
        RecipeCategory? category,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<RecipeSheet>()
            .AsNoTracking()
            .Include(recipe => recipe.Ingredients)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(recipe => recipe.IsActive);
        }

        if (category.HasValue)
        {
            query = query.Where(recipe => recipe.Category == category.Value);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();

        if (normalizedSearch is not null)
        {
            query = query.Where(recipe =>
                recipe.Code.Contains(normalizedSearch) ||
                recipe.Name.ToUpper().Contains(normalizedSearch));
        }

        var recipes = await query
            .OrderBy(recipe => recipe.Category)
            .ThenBy(recipe => recipe.Code)
            .ToArrayAsync(cancellationToken);

        return recipes.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<RecipeResponse>> GetRecipeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var recipe = await LoadRecipeAsync(code, track: false, cancellationToken);

        if (recipe is null)
        {
            return ApplicationResult<RecipeResponse>.NotFound("Recipe was not found.");
        }

        return ApplicationResult<RecipeResponse>.Success(Map(recipe));
    }

    public async Task<ApplicationResult<RecipeResponse>> CreateRecipeAsync(
        CreateRecipeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<RecipeResponse>.Validation("Recipe code is required.");
        }

        var exists = await dbContext.Set<RecipeSheet>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<RecipeResponse>.Conflict("A recipe with this code already exists.");
        }

        RecipeSheet recipe;

        try
        {
            recipe = new RecipeSheet(
                normalizedCode,
                request.Name,
                request.Category,
                request.YieldPortions,
                request.Allergens,
                request.Instructions);

            recipe.ReplaceIngredients(BuildIngredients(request.Ingredients));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RecipeResponse>.Validation(ex.Message);
        }

        var unknownItem = await FindUnknownItemCodeAsync(recipe.Ingredients, cancellationToken);

        if (unknownItem is not null)
        {
            return ApplicationResult<RecipeResponse>.Validation(
                $"Item {unknownItem} is unknown to the stock module. Ingredients must reference existing stock items.");
        }

        recipe.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<RecipeSheet>().Add(recipe);

        try
        {
            await WriteAuditAsync(
                "kitchen.recipe.created",
                RecipesEntity,
                recipe.Id,
                context,
                new { recipe.Code, recipe.Name, Category = recipe.Category.ToString(), recipe.YieldPortions },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The exists-check above and this insert are not atomic: a concurrent create with
            // the same code loses the race against ux_recipe_sheets_code.
            return ApplicationResult<RecipeResponse>.Conflict("A recipe with this code already exists.");
        }

        return ApplicationResult<RecipeResponse>.Success(Map(recipe));
    }

    public async Task<ApplicationResult<RecipeResponse>> UpdateRecipeAsync(
        string code,
        UpdateRecipeRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var recipe = await LoadRecipeAsync(code, track: true, cancellationToken);

        if (recipe is null)
        {
            return ApplicationResult<RecipeResponse>.NotFound("Recipe was not found.");
        }

        try
        {
            recipe.UpdateDetails(
                request.Name,
                request.Category,
                request.YieldPortions,
                request.Allergens,
                request.Instructions);

            recipe.ReplaceIngredients(BuildIngredients(request.Ingredients));
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<RecipeResponse>.Validation(ex.Message);
        }

        var unknownItem = await FindUnknownItemCodeAsync(recipe.Ingredients, cancellationToken);

        if (unknownItem is not null)
        {
            return ApplicationResult<RecipeResponse>.Validation(
                $"Item {unknownItem} is unknown to the stock module. Ingredients must reference existing stock items.");
        }

        recipe.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "kitchen.recipe.updated",
            RecipesEntity,
            recipe.Id,
            context,
            new { recipe.Code, recipe.Name, IngredientCount = recipe.Ingredients.Count, recipe.YieldPortions },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RecipeResponse>.Success(Map(recipe));
    }

    public async Task<ApplicationResult<RecipeResponse>> SetRecipeActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var recipe = await LoadRecipeAsync(code, track: true, cancellationToken);

        if (recipe is null)
        {
            return ApplicationResult<RecipeResponse>.NotFound("Recipe was not found.");
        }

        if (isActive)
        {
            recipe.Activate();
        }
        else
        {
            recipe.Deactivate();
        }

        recipe.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "kitchen.recipe.activated" : "kitchen.recipe.deactivated",
            RecipesEntity,
            recipe.Id,
            context,
            new { recipe.Code, recipe.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<RecipeResponse>.Success(Map(recipe));
    }

    public async Task<ApplicationResult<RecipeCostResponse>> GetRecipeCostAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var recipe = await LoadRecipeAsync(code, track: false, cancellationToken);

        if (recipe is null)
        {
            return ApplicationResult<RecipeCostResponse>.NotFound("Recipe was not found.");
        }

        var lines = new List<RecipeIngredientCostResponse>(recipe.Ingredients.Count);
        var missingItemCodes = new List<string>();
        var totalCost = 0m;

        foreach (var ingredient in recipe.Ingredients.OrderBy(current => current.LineNumber))
        {
            var costResult = await stockCostProvider.GetAverageCostAsync(ingredient.ItemCode, cancellationToken);

            // An ingredient without a known cost - item unknown to the stock module, or item
            // that never entered stock (cost zero or negative, defensively) - is flagged and
            // EXCLUDED from the total: an honest lower bound beats a silently wrong figure.
            var hasCost = costResult.Succeeded
                && costResult.Value is not null
                && costResult.Value.AverageUnitCost > 0m;

            if (!hasCost)
            {
                missingItemCodes.Add(ingredient.ItemCode);

                lines.Add(new RecipeIngredientCostResponse(
                    ingredient.LineNumber,
                    ingredient.ItemCode,
                    ingredient.Quantity,
                    costResult.Value?.UnitOfMeasure,
                    AverageUnitCost: null,
                    LineCost: null,
                    HasCost: false));

                continue;
            }

            var unitCost = costResult.Value!.AverageUnitCost;
            var lineCost = RoundMoney(ingredient.Quantity * unitCost);
            totalCost += lineCost;

            lines.Add(new RecipeIngredientCostResponse(
                ingredient.LineNumber,
                ingredient.ItemCode,
                ingredient.Quantity,
                costResult.Value.UnitOfMeasure,
                unitCost,
                lineCost,
                HasCost: true));
        }

        var hasMissingCosts = missingItemCodes.Count > 0;

        var warning = hasMissingCosts
            ? "Cout partiel : aucun cout moyen connu pour " + string.Join(", ", missingItemCodes) +
              " (article jamais entre en stock). Ces ingredients sont exclus du total, qui est donc un minorant."
            : null;

        var costPerPortion = RoundMoney(totalCost / recipe.YieldPortions);

        return ApplicationResult<RecipeCostResponse>.Success(new RecipeCostResponse(
            recipe.Code,
            recipe.Name,
            recipe.YieldPortions,
            lines,
            totalCost,
            costPerPortion,
            hasMissingCosts,
            warning,
            DateTimeOffset.UtcNow,
            CostBasisNote));
    }

    // ============================== HACCP checkpoints ==============================

    public async Task<IReadOnlyCollection<TemperatureCheckpointResponse>> ListCheckpointsAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<TemperatureCheckpoint>().AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(checkpoint => checkpoint.IsActive);
        }

        var checkpoints = await query
            .OrderBy(checkpoint => checkpoint.Code)
            .ToArrayAsync(cancellationToken);

        return checkpoints.Select(Map).ToArray();
    }

    public async Task<ApplicationResult<TemperatureCheckpointResponse>> CreateCheckpointAsync(
        CreateTemperatureCheckpointRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.Code);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<TemperatureCheckpointResponse>.Validation("Checkpoint code is required.");
        }

        var exists = await dbContext.Set<TemperatureCheckpoint>()
            .AnyAsync(current => current.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            return ApplicationResult<TemperatureCheckpointResponse>.Conflict(
                "A checkpoint with this code already exists.");
        }

        TemperatureCheckpoint checkpoint;

        try
        {
            checkpoint = new TemperatureCheckpoint(normalizedCode, request.Label, request.MinTemp, request.MaxTemp);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<TemperatureCheckpointResponse>.Validation(ex.Message);
        }

        checkpoint.MarkCreated(context.UserName, DateTimeOffset.UtcNow);
        dbContext.Set<TemperatureCheckpoint>().Add(checkpoint);

        try
        {
            await WriteAuditAsync(
                "kitchen.checkpoint.created",
                CheckpointsEntity,
                checkpoint.Id,
                context,
                new { checkpoint.Code, checkpoint.Label, checkpoint.MinTemp, checkpoint.MaxTemp },
                cancellationToken);

            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return ApplicationResult<TemperatureCheckpointResponse>.Conflict(
                "A checkpoint with this code already exists.");
        }

        return ApplicationResult<TemperatureCheckpointResponse>.Success(Map(checkpoint));
    }

    public async Task<ApplicationResult<TemperatureCheckpointResponse>> UpdateCheckpointAsync(
        string code,
        UpdateTemperatureCheckpointRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var checkpoint = await LoadCheckpointAsync(code, track: true, cancellationToken);

        if (checkpoint is null)
        {
            return ApplicationResult<TemperatureCheckpointResponse>.NotFound("Checkpoint was not found.");
        }

        try
        {
            // Editing the thresholds only affects FUTURE readings: past readings carry their
            // own frozen snapshot of the range they were judged against (see TemperatureReading).
            checkpoint.Update(request.Label, request.MinTemp, request.MaxTemp);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<TemperatureCheckpointResponse>.Validation(ex.Message);
        }

        checkpoint.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            "kitchen.checkpoint.updated",
            CheckpointsEntity,
            checkpoint.Id,
            context,
            new { checkpoint.Code, checkpoint.Label, checkpoint.MinTemp, checkpoint.MaxTemp },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<TemperatureCheckpointResponse>.Success(Map(checkpoint));
    }

    public async Task<ApplicationResult<TemperatureCheckpointResponse>> SetCheckpointActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var checkpoint = await LoadCheckpointAsync(code, track: true, cancellationToken);

        if (checkpoint is null)
        {
            return ApplicationResult<TemperatureCheckpointResponse>.NotFound("Checkpoint was not found.");
        }

        if (isActive)
        {
            checkpoint.Activate();
        }
        else
        {
            checkpoint.Deactivate();
        }

        checkpoint.MarkUpdated(context.UserName, DateTimeOffset.UtcNow);

        await WriteAuditAsync(
            isActive ? "kitchen.checkpoint.activated" : "kitchen.checkpoint.deactivated",
            CheckpointsEntity,
            checkpoint.Id,
            context,
            new { checkpoint.Code, checkpoint.IsActive },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<TemperatureCheckpointResponse>.Success(Map(checkpoint));
    }

    // ============================== HACCP readings ==============================

    public async Task<IReadOnlyCollection<TemperatureReadingResponse>> ListReadingsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? checkpointCode,
        bool nonCompliantOnly,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<TemperatureReading>().AsNoTracking();

        // The period bounds are written with CompareTo, not with the >= / <= operators: the
        // SQLite provider of the integration-test harness refuses to translate a DateTimeOffset
        // comparison, but does translate CompareTo, which the shared relational translator gives
        // both providers (same reason and same shape as ReportingService.LoadRecentExecutionsAsync).
        // Timestamps are always written as UTC, so the text form SQLite compares carries one
        // single offset and orders exactly like the PostgreSQL timestamptz comparison.
        if (from.HasValue)
        {
            var fromValue = from.Value;
            query = query.Where(reading => reading.MeasuredAt.CompareTo(fromValue) >= 0);
        }

        if (to.HasValue)
        {
            var toValue = to.Value;
            query = query.Where(reading => reading.MeasuredAt.CompareTo(toValue) <= 0);
        }

        var normalizedCheckpointCode = NormalizeNullableCode(checkpointCode);

        if (normalizedCheckpointCode is not null)
        {
            query = query.Where(reading => reading.CheckpointCode == normalizedCheckpointCode);
        }

        if (nonCompliantOnly)
        {
            query = query.Where(reading => !reading.IsCompliant);
        }

        // Ordering cannot be pushed to the database either: SQLite refuses ORDER BY on a
        // DateTimeOffset column outright ("SQLite doesn't support expressions of type
        // 'DateTimeOffset' in ORDER BY clauses"). The rows of a filtered period are sorted in
        // memory, most recent first - the order the HACCP log is read in.
        var readings = await query.ToArrayAsync(cancellationToken);

        var ordered = readings
            .OrderByDescending(reading => reading.MeasuredAt)
            .ThenBy(reading => reading.CheckpointCode, StringComparer.Ordinal)
            .ToArray();

        var labels = await LoadCheckpointLabelsAsync(
            ordered.Select(reading => reading.CheckpointCode).Distinct().ToArray(),
            cancellationToken);

        return ordered
            .Select(reading => Map(reading, labels.GetValueOrDefault(reading.CheckpointCode)))
            .ToArray();
    }

    public async Task<ApplicationResult<TemperatureReadingResponse>> CreateReadingAsync(
        CreateTemperatureReadingRequest request,
        OperationContext context,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(request.CheckpointCode);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ApplicationResult<TemperatureReadingResponse>.Validation("Checkpoint code is required.");
        }

        var checkpoint = await dbContext.Set<TemperatureCheckpoint>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);

        if (checkpoint is null)
        {
            return ApplicationResult<TemperatureReadingResponse>.NotFound("Checkpoint was not found.");
        }

        if (!checkpoint.IsActive)
        {
            return ApplicationResult<TemperatureReadingResponse>.Validation(
                "Readings cannot be recorded on an inactive checkpoint.");
        }

        var now = DateTimeOffset.UtcNow;
        var measuredAt = request.MeasuredAt ?? now;

        // A reading transcribed from a paper log may legitimately be in the past; a reading in
        // the future is a keying error (small tolerance for clock skew between client and server).
        if (measuredAt > now.AddMinutes(5))
        {
            return ApplicationResult<TemperatureReadingResponse>.Validation(
                "A temperature reading cannot be dated in the future.");
        }

        TemperatureReading reading;

        try
        {
            reading = new TemperatureReading(
                checkpoint,
                request.ValueCelsius,
                context.UserName,
                measuredAt,
                request.CorrectiveAction);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return ApplicationResult<TemperatureReadingResponse>.Validation(ex.Message);
        }

        reading.MarkCreated(context.UserName, now);
        dbContext.Set<TemperatureReading>().Add(reading);

        await WriteAuditAsync(
            "kitchen.reading.recorded",
            ReadingsEntity,
            reading.Id,
            context,
            new
            {
                reading.CheckpointCode,
                reading.ValueCelsius,
                reading.IsCompliant,
                reading.MinTempSnapshot,
                reading.MaxTempSnapshot,
                reading.CorrectiveAction
            },
            cancellationToken);

        await SaveAsync(cancellationToken);

        return ApplicationResult<TemperatureReadingResponse>.Success(Map(reading, checkpoint.Label));
    }

    // ============================== Helpers ==============================

    /// <summary>
    /// Existence check of the ingredient item codes through the shared stock contract: the
    /// provider answers NotFound for an unknown item and Success for an existing one - whether
    /// or not it already has a cost (an item never received yet is a legitimate ingredient; it
    /// simply shows up without a cost in GetRecipeCostAsync). Returns the first unknown code,
    /// or null when every ingredient is known.
    /// </summary>
    private async Task<string?> FindUnknownItemCodeAsync(
        IReadOnlyCollection<RecipeIngredient> ingredients,
        CancellationToken cancellationToken)
    {
        foreach (var itemCode in ingredients.Select(ingredient => ingredient.ItemCode).Distinct())
        {
            var result = await stockCostProvider.GetAverageCostAsync(itemCode, cancellationToken);

            if (result.ErrorType == ApplicationErrorType.NotFound)
            {
                return itemCode;
            }
        }

        return null;
    }

    private async Task<RecipeSheet?> LoadRecipeAsync(string code, bool track, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = dbContext.Set<RecipeSheet>()
            .Include(current => current.Ingredients)
            .AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private async Task<TemperatureCheckpoint?> LoadCheckpointAsync(
        string code,
        bool track,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCodeOrEmpty(code);

        var query = dbContext.Set<TemperatureCheckpoint>().AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(current => current.Code == normalizedCode, cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadCheckpointLabelsAsync(
        string[] checkpointCodes,
        CancellationToken cancellationToken)
    {
        if (checkpointCodes.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        return await dbContext.Set<TemperatureCheckpoint>()
            .AsNoTracking()
            .Where(checkpoint => checkpointCodes.Contains(checkpoint.Code))
            .ToDictionaryAsync(checkpoint => checkpoint.Code, checkpoint => checkpoint.Label, cancellationToken);
    }

    private static List<RecipeIngredient> BuildIngredients(IReadOnlyCollection<RecipeIngredientRequest>? requests)
    {
        if (requests is null)
        {
            return new List<RecipeIngredient>();
        }

        return requests
            .Select(ingredient => new RecipeIngredient(ingredient.ItemCode, ingredient.Quantity, ingredient.Notes))
            .ToList();
    }

    private static RecipeResponse Map(RecipeSheet recipe)
    {
        var ingredients = recipe.Ingredients
            .OrderBy(ingredient => ingredient.LineNumber)
            .Select(ingredient => new RecipeIngredientResponse(
                ingredient.Id,
                ingredient.LineNumber,
                ingredient.ItemCode,
                ingredient.Quantity,
                ingredient.Notes))
            .ToArray();

        return new RecipeResponse(
            recipe.Id,
            recipe.Code,
            recipe.Name,
            recipe.Category,
            recipe.YieldPortions,
            recipe.Allergens,
            recipe.Instructions,
            recipe.IsActive,
            ingredients,
            recipe.CreatedAt,
            recipe.CreatedBy,
            recipe.UpdatedAt,
            recipe.UpdatedBy);
    }

    private static TemperatureCheckpointResponse Map(TemperatureCheckpoint checkpoint)
    {
        return new TemperatureCheckpointResponse(
            checkpoint.Id,
            checkpoint.Code,
            checkpoint.Label,
            checkpoint.MinTemp,
            checkpoint.MaxTemp,
            checkpoint.IsActive,
            checkpoint.CreatedAt,
            checkpoint.CreatedBy,
            checkpoint.UpdatedAt,
            checkpoint.UpdatedBy);
    }

    private static TemperatureReadingResponse Map(TemperatureReading reading, string? checkpointLabel)
    {
        return new TemperatureReadingResponse(
            reading.Id,
            reading.CheckpointCode,
            checkpointLabel,
            reading.MeasuredAt,
            reading.ValueCelsius,
            reading.RecordedBy,
            reading.MinTempSnapshot,
            reading.MaxTempSnapshot,
            reading.IsCompliant,
            reading.CorrectiveAction,
            reading.CreatedAt);
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCodeOrEmpty(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private static string? NormalizeNullableCode(string? code)
    {
        return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Explicit flush after the audit write. AuditLogWriter.WriteAsync already calls
    /// SaveChangesAsync internally, so this call is usually a no-op - it exists so persistence
    /// never silently depends on the audit writer's internals (same note as BillingService).
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        string action,
        string entityName,
        Guid entityId,
        OperationContext context,
        object details,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditLogEntry(
                context.UserId,
                context.UserName,
                action,
                entityName,
                entityId.ToString(),
                context.IpAddress,
                JsonSerializer.Serialize(details)),
            cancellationToken);
    }
}
