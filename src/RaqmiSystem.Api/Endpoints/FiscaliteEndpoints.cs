using RaqmiSystem.Application.Fiscalite;
using RaqmiSystem.Domain.Identity;

namespace RaqmiSystem.Api.Endpoints;

/// <summary>
/// Fiscalite DGI &amp; SIFEC: VAT registers, monthly declaration, G50 export, withholding tax,
/// annual fiscal return, and the SIFEC e-invoicing connector.
///
/// Three permissions: finance.fiscal.read (every register/declaration/return/hub GET),
/// finance.fiscal.declare (calculating a declaration, exporting G50, marking one declared,
/// registering a withholding entry, generating a fiscal return) and finance.fiscal.sifec.manage
/// (SIFEC config and transmissions - restricted, matching the admin-only restriction the legacy
/// product applied to the whole module).
/// </summary>
internal static class FiscaliteEndpoints
{
    public static RouteGroupBuilder MapFiscaliteEndpoints(this RouteGroupBuilder api)
    {
        MapRegisterEndpoints(api);
        MapDeclarationEndpoints(api);
        MapWithholdingTaxEndpoints(api);
        MapFiscalReturnEndpoints(api);
        MapSifecEndpoints(api);
        return api;
    }

    private static void MapRegisterEndpoints(RouteGroupBuilder api)
    {
        var sales = api.MapGroup("/fiscalite/registre-tva-ventes").WithTags("Fiscalite - registre TVA ventes");
        sales.MapGet("", async (int year, int month, IVatRegisterService s, CancellationToken ct) =>
            Results.Ok(await s.GetSalesRegisterAsync(year, month, ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        var purchases = api.MapGroup("/fiscalite/registre-tva-achats").WithTags("Fiscalite - registre TVA achats");
        purchases.MapGet("", async (int year, int month, IVatRegisterService s, CancellationToken ct) =>
            Results.Ok(await s.GetPurchasesRegisterAsync(year, month, ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        purchases.MapPost("", async (CreateVatPurchaseEntryRequest request, IVatRegisterService s, HttpContext h, CancellationToken ct) =>
        {
            var result = await s.CreateManualPurchaseEntryAsync(request, h.ToOperationContext(), ct);
            return result.Succeeded && result.Value is not null
                ? Results.Created("/api/v1/fiscalite/registre-tva-achats", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);

        purchases.MapPost("/import", async (int year, int month, IVatRegisterService s, HttpContext h, CancellationToken ct) =>
            (await s.ImportApprovedPurchaseOrdersAsync(year, month, h.ToOperationContext(), ct))
                .ToHttpResult()).RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);
    }

    private static void MapDeclarationEndpoints(RouteGroupBuilder api)
    {
        var declarations = api.MapGroup("/fiscalite/declarations").WithTags("Fiscalite - declaration TVA");
        declarations.MapGet("", async (IVatDeclarationService s, CancellationToken ct) =>
            Results.Ok(await s.GetHistoryAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        declarations.MapPost("/calculate", async (int year, int month, IVatDeclarationService s, HttpContext h, CancellationToken ct) =>
            (await s.CalculateAsync(year, month, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);

        var teleDeclarations = api.MapGroup("/fiscalite/teledeclarations").WithTags("Fiscalite - teledeclarations");
        teleDeclarations.MapGet("", async (IVatDeclarationService s, CancellationToken ct) =>
            Results.Ok(await s.GetTeleDeclarationsAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        teleDeclarations.MapPost("/export-g50", async (int year, int month, IVatDeclarationService s, HttpContext h, CancellationToken ct) =>
            (await s.ExportG50Async(year, month, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);

        teleDeclarations.MapPost("/{id:guid}/marquer-declaree", async (Guid id, MarkDeclaredRequest request, IVatDeclarationService s, HttpContext h, CancellationToken ct) =>
            (await s.MarkDeclaredAsync(id, request.DgiReference, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);
    }

    private static void MapWithholdingTaxEndpoints(RouteGroupBuilder api)
    {
        var withholding = api.MapGroup("/fiscalite/retenues-source").WithTags("Fiscalite - retenue a la source");
        withholding.MapGet("", async (IWithholdingTaxService s, CancellationToken ct) =>
            Results.Ok(await s.ListAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        withholding.MapPost("", async (CreateWithholdingTaxEntryRequest request, IWithholdingTaxService s, HttpContext h, CancellationToken ct) =>
        {
            var result = await s.RegisterAsync(request, h.ToOperationContext(), ct);
            return result.Succeeded && result.Value is not null
                ? Results.Created("/api/v1/fiscalite/retenues-source", result.Value)
                : result.ToHttpResult();
        }).RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);
    }

    private static void MapFiscalReturnEndpoints(RouteGroupBuilder api)
    {
        var liasse = api.MapGroup("/fiscalite/liasse").WithTags("Fiscalite - liasse fiscale");
        liasse.MapGet("", async (int year, IFiscalReturnService s, CancellationToken ct) =>
            Results.Ok(await s.GetAsync(year, ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        liasse.MapPost("/generer", async (int year, IFiscalReturnService s, HttpContext h, CancellationToken ct) =>
            (await s.GenerateSimpleAsync(year, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);

        liasse.MapPost("/generer-avancee", async (int year, IFiscalReturnService s, HttpContext h, CancellationToken ct) =>
            (await s.GenerateAdvancedAsync(year, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalDeclare);
    }

    private static void MapSifecEndpoints(RouteGroupBuilder api)
    {
        var sifec = api.MapGroup("/fiscalite/sifec").WithTags("Fiscalite - SIFEC");
        sifec.MapGet("/hub", async (ISifecService s, CancellationToken ct) =>
            Results.Ok(await s.GetHubSummaryAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        var factures = api.MapGroup("/fiscalite/sifec/factures").WithTags("Fiscalite - SIFEC transmissions");
        factures.MapGet("", async (ISifecService s, CancellationToken ct) =>
            Results.Ok(await s.ListTransmissionsAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalRead);

        factures.MapPost("/transmettre-lot", async (ISifecService s, HttpContext h, CancellationToken ct) =>
            (await s.SubmitBatchAsync(h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalSifecManage);

        factures.MapPost("/{invoiceId:guid}/envoyer", async (Guid invoiceId, ISifecService s, HttpContext h, CancellationToken ct) =>
            (await s.SubmitAsync(invoiceId, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalSifecManage);

        var config = api.MapGroup("/fiscalite/sifec/config").WithTags("Fiscalite - SIFEC config");
        config.MapGet("", async (ISifecService s, CancellationToken ct) =>
            Results.Ok(await s.GetConfigAsync(ct))).RequireAuthorization(PermissionCatalog.FinanceFiscalSifecManage);

        config.MapPut("", async (SaveSifecConfigRequest request, ISifecService s, HttpContext h, CancellationToken ct) =>
            (await s.SaveConfigAsync(request, h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalSifecManage);

        config.MapPost("/test-connexion", async (ISifecService s, HttpContext h, CancellationToken ct) =>
            (await s.TestConnectionAsync(h.ToOperationContext(), ct)).ToHttpResult())
            .RequireAuthorization(PermissionCatalog.FinanceFiscalSifecManage);
    }
}

public sealed record MarkDeclaredRequest(string DgiReference);
