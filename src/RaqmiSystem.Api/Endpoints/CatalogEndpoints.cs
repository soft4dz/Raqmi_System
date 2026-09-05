using RaqmiSystem.Application.Catalog;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Catalogue des articles vendables : le referentiel COMMERCIAL (prix de vente HT, taux de TVA,
/// lien optionnel vers un article de stock) sur lequel la facturation construit ses lignes.
///
/// PERMISSIONS : le catalogue est un referentiel de la facturation, il en porte les cles.
/// billing.invoice.read pour consulter, billing.invoice.manage pour creer et modifier ; chaque
/// politique accepte encore la cle historique qui la couvre (invoices.read / invoices.write,
/// voir PermissionRegistry). Aucune cle nouvelle : celui qui redige des factures choisit ce
/// qu'il vend.
/// </summary>
internal static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var articles = api.MapGroup("/catalog/articles")
            .WithTags("Catalog");

        articles.MapGet("", async (
            string? search,
            string? family,
            bool? includeInactive,
            ICatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ListArticlesAsync(search, family, includeInactive == true, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceRead);

        articles.MapGet("/{code}", async (
            string code,
            ICatalogService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetArticleAsync(code, cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceRead);

        articles.MapPost("", async (
            CreateArticleRequest request,
            ICatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreateArticleAsync(request, httpContext.ToOperationContext(), cancellationToken);

            return result.Succeeded && result.Value is not null
                ? Results.Created($"/api/v1/catalog/articles/{result.Value.Code}", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceManage);

        articles.MapPut("/{code}", async (
            string code,
            UpdateArticleRequest request,
            ICatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateArticleAsync(code, request, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceManage);

        articles.MapPost("/{code}/activate", async (
            string code,
            ICatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetArticleActiveAsync(code, true, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceManage);

        articles.MapPost("/{code}/deactivate", async (
            string code,
            ICatalogService service,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var result = await service.SetArticleActiveAsync(code, false, httpContext.ToOperationContext(), cancellationToken);
            return result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.BillingInvoiceManage);

        return api;
    }
}
