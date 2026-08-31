using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Stocks &amp; consommations (11) - magasins par unite, articles avec seuil d'alerte,
/// registre des mouvements (entrees, sorties, transferts, ajustements), stock courant valorise
/// au cout moyen pondere, alertes sous-seuil et inventaires physiques.
///
/// Doctrine rappelee ici parce qu'elle explique le decoupage des routes : le stock n'est JAMAIS
/// une colonne, c'est la SOMME du registre des mouvements. Il n'existe donc aucune route qui
/// "fixe" un stock : on ajoute un mouvement, ou on valide un inventaire qui genere les
/// mouvements d'ajustement. Un transfert n'a pas non plus de route "demi-transfert" : POST
/// /inventory/transfers cree les deux moities liees en une seule transaction.
///
/// Permissions, categorie "exploitation" de PermissionCatalog : inventory.read pour direction,
/// exploitation.control, unit.manager et reader ; inventory.write pour exploitation.control et
/// unit.manager ; inventory.validate pour direction et exploitation.control seulement - valider
/// un inventaire fige des ajustements de stock, c'est un acte de controle, separe de la saisie
/// quotidienne du magasinier (system.administrator recoit tout via le catch-all de
/// PermissionCatalog.All). Program.cs enregistre une policy par cle du catalogue, et
/// DependencyInjection.cs sert les TROIS contrats depuis la meme classe - IInventoryService,
/// IStockOperationService et IStockCostProvider -> InventoryService, ces deux derniers etant
/// CONSOMMES par les modules Achats et Cuisine de la meme vague. Schema "inventory" cree par la
/// migration WaveInventory.
/// </summary>
internal static class InventoryEndpoints
{
    private const string Read = PermissionCatalog.InventoryRead;

    private const string Write = PermissionCatalog.InventoryWrite;

    private const string Validate = PermissionCatalog.InventoryValidate;

    public static RouteGroupBuilder MapInventoryEndpoints(this RouteGroupBuilder api)
    {
        MapWarehouseEndpoints(api);
        MapItemEndpoints(api);
        MapMovementEndpoints(api);
        MapStockEndpoints(api);
        MapCountEndpoints(api);
        return api;
    }

    private static void MapWarehouseEndpoints(RouteGroupBuilder api)
    {
        var warehouses = api.MapGroup("/inventory/warehouses")
            .WithTags("Inventory");

        warehouses.MapGet("", async (
            bool? includeInactive,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListWarehousesAsync(includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(Read);

        // Stock courant d'un magasin : quantite par article (somme du registre), cout moyen
        // pondere, valeur, drapeau sous-seuil, et valorisation totale calculee cote serveur.
        warehouses.MapGet("/{code}/stock", async (
            string code,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetWarehouseStockAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Read);

        warehouses.MapPost("", async (
            CreateWarehouseRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateWarehouseAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/inventory/warehouses/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(Write);

        warehouses.MapPut("/{code}", async (
            string code,
            UpdateWarehouseRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateWarehouseAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        warehouses.MapPost("/{code}/activate", async (
            string code,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetWarehouseActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        warehouses.MapPost("/{code}/deactivate", async (
            string code,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetWarehouseActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);
    }

    private static void MapItemEndpoints(RouteGroupBuilder api)
    {
        var items = api.MapGroup("/inventory/items")
            .WithTags("Inventory");

        items.MapGet("", async (
            string? search,
            bool? includeInactive,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListItemsAsync(search, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(Read);

        items.MapPost("", async (
            CreateStockItemRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateItemAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/inventory/items/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(Write);

        items.MapPut("/{code}", async (
            string code,
            UpdateStockItemRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateItemAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        items.MapPost("/{code}/activate", async (
            string code,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetItemActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        items.MapPost("/{code}/deactivate", async (
            string code,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetItemActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);
    }

    private static void MapMovementEndpoints(RouteGroupBuilder api)
    {
        var movements = api.MapGroup("/inventory/movements")
            .WithTags("Inventory");

        movements.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? warehouseCode,
            string? itemCode,
            string? kind,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseKind(kind, out var parsedKind, out var kindError))
            {
                return Results.BadRequest(new ErrorResponse(kindError));
            }

            var result = await service.ListMovementsAsync(
                from,
                to,
                warehouseCode,
                itemCode,
                parsedKind,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(Read);

        // Saisie directe d'UN mouvement : entree d'achat, sortie de consommation ou ajustement
        // manuel. Les deux moities d'un transfert sont refusees ici (voir /inventory/transfers).
        movements.MapPost("", async (
            CreateStockMovementRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateMovementAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        var transfers = api.MapGroup("/inventory/transfers")
            .WithTags("Inventory");

        transfers.MapPost("", async (
            CreateStockTransferRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.TransferAsync(request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);
    }

    private static void MapStockEndpoints(RouteGroupBuilder api)
    {
        var stock = api.MapGroup("/inventory")
            .WithTags("Inventory");

        stock.MapGet("/low-stock", async (
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetLowStockAsync(cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(Read);
    }

    private static void MapCountEndpoints(RouteGroupBuilder api)
    {
        var counts = api.MapGroup("/inventory/counts")
            .WithTags("Inventory");

        counts.MapGet("", async (
            string? warehouseCode,
            string? status,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseCountStatus(status, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListCountsAsync(warehouseCode, parsedStatus, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(Read);

        counts.MapGet("/{id:guid}", async (
            Guid id,
            IInventoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetCountAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Read);

        counts.MapPost("", async (
            CreateInventoryCountRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateCountAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/inventory/counts/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(Write);

        counts.MapPut("/{id:guid}/lines", async (
            Guid id,
            ReplaceInventoryCountLinesRequest request,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReplaceCountLinesAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Write);

        // Acte engageant : genere les mouvements d'ajustement puis fige l'inventaire pour
        // toujours. Droit distinct de l'ecriture (separation des taches : le magasinier compte,
        // le controle valide).
        counts.MapPost("/{id:guid}/validate", async (
            Guid id,
            IInventoryService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ValidateCountAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(Validate);
    }

    private static bool TryParseKind(string? kind, out StockMovementKind? parsedKind, out string error)
    {
        parsedKind = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(kind))
        {
            return true;
        }

        if (Enum.TryParse<StockMovementKind>(kind.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedKind = value;
            return true;
        }

        error = "Movement kind must be PurchaseEntry, Consumption, TransferOut, TransferIn or InventoryAdjustment.";
        return false;
    }

    private static bool TryParseCountStatus(string? status, out InventoryCountStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<InventoryCountStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Inventory count status must be Draft or Validated.";
        return false;
    }
}
