using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Application.Kitchen;

/// <summary>
/// Module Cuisine, production &amp; qualite - honest scope: recipe sheets with on-demand
/// material cost (through the stock module's <c>IStockCostProvider</c> contract) and HACCP
/// temperature readings with frozen compliance verdicts. Menu engineering, waste tracking and
/// full batch traceability are OUT of this module's scope.
/// </summary>
public interface IKitchenService
{
    Task<IReadOnlyCollection<RecipeResponse>> ListRecipesAsync(
        string? search,
        RecipeCategory? category,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RecipeResponse>> GetRecipeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RecipeResponse>> CreateRecipeAsync(
        CreateRecipeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RecipeResponse>> UpdateRecipeAsync(
        string code,
        UpdateRecipeRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RecipeResponse>> SetRecipeActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Material cost of the recipe: for each ingredient, quantity times the CURRENT weighted
    /// average cost served by the stock module, plus the cost per portion. An ingredient
    /// without a known cost is flagged and excluded from the total with a warning - never
    /// silently costed at zero.
    /// </summary>
    Task<ApplicationResult<RecipeCostResponse>> GetRecipeCostAsync(
        string code,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TemperatureCheckpointResponse>> ListCheckpointsAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TemperatureCheckpointResponse>> CreateCheckpointAsync(
        CreateTemperatureCheckpointRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TemperatureCheckpointResponse>> UpdateCheckpointAsync(
        string code,
        UpdateTemperatureCheckpointRequest request,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TemperatureCheckpointResponse>> SetCheckpointActiveAsync(
        string code,
        bool isActive,
        OperationContext context,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TemperatureReadingResponse>> ListReadingsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? checkpointCode,
        bool nonCompliantOnly,
        CancellationToken cancellationToken);

    Task<ApplicationResult<TemperatureReadingResponse>> CreateReadingAsync(
        CreateTemperatureReadingRequest request,
        OperationContext context,
        CancellationToken cancellationToken);
}
