using RaqmiSystem.Application.Purchasing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Module Achats &amp; approvisionnements (module 12) - PERIMETRE HONNETE : referentiel
/// fournisseurs, bons de commande numerotes a l'approbation et receptions (partielles ou
/// totales) qui alimentent le stock. Les demandes d'achat, les consultations / demandes de
/// prix et les factures fournisseurs sont HORS PERIMETRE de ce module.
///
/// Permissions, categorie "exploitation" de PermissionCatalog : purchasing.read pour direction,
/// exploitation.control, unit.manager et reader ; purchasing.write pour exploitation.control et
/// unit.manager ; purchasing.approve pour direction et exploitation.control ;
/// purchasing.receive pour exploitation.control et unit.manager (system.administrator recoit
/// tout via le catch-all de PermissionCatalog.All). Schema "purchasing" cree par la migration
/// WaveInventory. Ce module CONSOMME IStockOperationService et IStockCostProvider (contrats
/// publies par le module Stocks) et n'enregistre donc pas ces interfaces lui-meme.
///
/// Decoupage des droits, motive :
///   - "purchasing.approve" est DISTINCT de "purchasing.write" parce que l'approbation engage
///     la depense : elle alloue le numero definitif "BC-{annee}-{seq:D6}" et fige les lignes.
///     Saisir un brouillon et engager l'entreprise ne sont pas le meme geste.
///   - "purchasing.receive" est distinct des deux autres parce que la reception est un geste
///     de magasin, pas d'achat : elle genere de vraies entrees en stock. Meme motif que
///     "lodging.checkin" face a "lodging.write".
/// </summary>
internal static class PurchasingEndpoints
{
    public static RouteGroupBuilder MapPurchasingEndpoints(this RouteGroupBuilder api)
    {
        MapSupplierEndpoints(api);
        MapPurchaseOrderEndpoints(api);
        return api;
    }

    private static void MapSupplierEndpoints(RouteGroupBuilder api)
    {
        var suppliers = api.MapGroup("/purchasing/suppliers")
            .WithTags("Suppliers");

        suppliers.MapGet("", async (
            string? search,
            bool? includeInactive,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListSuppliersAsync(search, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.PurchasingRead);

        suppliers.MapGet("/{code}", async (
            string code,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetSupplierAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingRead);

        suppliers.MapPost("", async (
            CreateSupplierRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateSupplierAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/purchasing/suppliers/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);

        suppliers.MapPut("/{code}", async (
            string code,
            UpdateSupplierRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateSupplierAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);

        suppliers.MapPost("/{code}/activate", async (
            string code,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetSupplierActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);

        suppliers.MapPost("/{code}/deactivate", async (
            string code,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetSupplierActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);
    }

    private static void MapPurchaseOrderEndpoints(RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/purchasing/orders")
            .WithTags("PurchaseOrders");

        orders.MapGet("", async (
            DateOnly? from,
            DateOnly? to,
            string? supplierCode,
            string? warehouseCode,
            string? status,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                return Results.BadRequest(new ErrorResponse("The from date cannot be after the to date."));
            }

            if (!TryParseStatus(status, out var parsedStatus, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error));
            }

            var result = await service.ListOrdersAsync(
                from,
                to,
                supplierCode,
                warehouseCode,
                parsedStatus,
                cancellationToken);

            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.PurchasingRead);

        orders.MapGet("/{id:guid}", async (
            Guid id,
            IPurchasingService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetOrderAsync(id, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingRead);

        // Un bon de commande nait en brouillon : SANS numero, lignes modifiables. Le numero
        // definitif n'est alloue qu'a l'approbation, pour qu'un brouillon abandonne ne brule
        // jamais un rang de la sequence.
        orders.MapPost("", async (
            CreatePurchaseOrderRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateOrderAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/purchasing/orders/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);

        // Reecriture des lignes : refusee (409) des que la commande est approuvee - les lignes
        // sont figees, c'est le document envoye au fournisseur.
        orders.MapPut("/{id:guid}/lines", async (
            Guid id,
            UpdatePurchaseOrderLinesRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateOrderLinesAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);

        // Acte engageant : alloue "BC-{annee}-{seq:D6}" et fige les lignes. Droit dedie.
        orders.MapPost("/{id:guid}/approve", async (
            Guid id,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveOrderAsync(id, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingApprove);

        // Une livraison : quantites recues MAINTENANT, ligne par ligne, cumulees cote serveur.
        // Autant de receptions que necessaire pour completer la commande ; la sur-reception est
        // refusee et le statut passe seul a PartiallyReceived puis Received.
        orders.MapPost("/{id:guid}/receive", async (
            Guid id,
            ReceivePurchaseOrderRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ReceiveOrderAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingReceive);

        // Annulation motivee, impossible des qu'une seule unite est entree en stock : la
        // commande est alors la piece justificative de mouvements de stock reels.
        orders.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelPurchaseOrderRequest request,
            IPurchasingService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CancelOrderAsync(id, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.PurchasingWrite);
    }

    private static bool TryParseStatus(string? status, out PurchaseOrderStatus? parsedStatus, out string error)
    {
        parsedStatus = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<PurchaseOrderStatus>(status.Trim(), ignoreCase: true, out var value) &&
            Enum.IsDefined(value))
        {
            parsedStatus = value;
            return true;
        }

        error = "Purchase order status must be Draft, Approved, PartiallyReceived, Received or Cancelled.";
        return false;
    }
}
