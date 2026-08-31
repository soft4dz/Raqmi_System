using RaqmiSystem.Application.Kitchen;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Cuisine, production &amp; qualite (11.5) - perimetre honnete : fiches techniques
/// avec cout matiere calcule a la demande (PMP courants du module Stocks, via le contrat
/// IStockCostProvider) et releves de temperature HACCP avec conformite figee au moment du
/// releve. Menu engineering, suivi du gaspillage et tracabilite complete des lots : HORS
/// PERIMETRE de ce module.
///
/// Permissions, categorie "exploitation" de PermissionCatalog : kitchen.read pour direction,
/// exploitation.control, unit.manager et reader ; kitchen.write pour exploitation.control et
/// unit.manager (system.administrator recoit tout via le catch-all de PermissionCatalog.All).
/// Schema "kitchen" cree par la migration WaveInventory. Ce module CONSOMME
/// IStockCostProvider (contrat publie par le module Stocks) et n'enregistre donc pas cette
/// interface lui-meme.
/// </summary>
internal static class KitchenEndpoints
{
    public static RouteGroupBuilder MapKitchenEndpoints(this RouteGroupBuilder api)
    {
        MapRecipeEndpoints(api);
        MapCheckpointEndpoints(api);
        MapReadingEndpoints(api);
        return api;
    }

    private static void MapRecipeEndpoints(RouteGroupBuilder api)
    {
        var recipes = api.MapGroup("/kitchen/recipes")
            .WithTags("Kitchen");

        recipes.MapGet("", async (
            string? search,
            string? category,
            bool? includeInactive,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseCategory(category, out var parsedCategory, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListRecipesAsync(
                search,
                parsedCategory,
                includeInactive == true,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        recipes.MapGet("/{code}", async (
            string code,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRecipeAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        // Cout matiere : quantite x PMP courant par ingredient, total et cout par portion.
        // Un ingredient sans cout connu est signale (hasCost=false) et exclu du total.
        recipes.MapGet("/{code}/cost", async (
            string code,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetRecipeCostAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        recipes.MapPost("", async (
            CreateRecipeRequest request,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRecipeAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/kitchen/recipes/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        recipes.MapPut("/{code}", async (
            string code,
            UpdateRecipeRequest request,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateRecipeAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        recipes.MapPost("/{code}/activate", async (
            string code,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRecipeActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        recipes.MapPost("/{code}/deactivate", async (
            string code,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetRecipeActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);
    }

    private static void MapCheckpointEndpoints(RouteGroupBuilder api)
    {
        var checkpoints = api.MapGroup("/kitchen/checkpoints")
            .WithTags("Kitchen");

        checkpoints.MapGet("", async (
            bool? includeInactive,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListCheckpointsAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        checkpoints.MapPost("", async (
            CreateTemperatureCheckpointRequest request,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCheckpointAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/kitchen/checkpoints/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        checkpoints.MapPut("/{code}", async (
            string code,
            UpdateTemperatureCheckpointRequest request,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateCheckpointAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        checkpoints.MapPost("/{code}/activate", async (
            string code,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCheckpointActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);

        checkpoints.MapPost("/{code}/deactivate", async (
            string code,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetCheckpointActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);
    }

    private static void MapReadingEndpoints(RouteGroupBuilder api)
    {
        var readings = api.MapGroup("/kitchen/readings")
            .WithTags("Kitchen");

        readings.MapGet("", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? checkpointCode,
            bool? nonCompliantOnly,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.ListReadingsAsync(
                from,
                to,
                checkpointCode,
                nonCompliantOnly == true,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        // Liste des non-conformites par periode : le meme flux que la liste generale,
        // restreint aux verdicts non conformes - un chemin dedie pour que le suivi qualite
        // soit une requete directe, pas une convention de parametre a connaitre.
        readings.MapGet("/non-compliant", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? checkpointCode,
            IKitchenService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            var result = await service.ListReadingsAsync(
                from,
                to,
                checkpointCode,
                nonCompliantOnly: true,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.KitchenRead);

        readings.MapPost("", async (
            CreateTemperatureReadingRequest request,
            IKitchenService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateReadingAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/kitchen/readings/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.KitchenWrite);
    }

    private static bool TryParseCategory(string? category, out RecipeCategory? parsedCategory, out string error)
    {
        parsedCategory = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        if (Enum.TryParse<RecipeCategory>(category.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedCategory = value;
            return true;
        }

        error = "Recipe category must be Entree, Plat, Dessert, Boisson or SousPreparation.";
        return false;
    }
}
