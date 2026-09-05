using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Inventory;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Tests;

/// <summary>
/// La chaine de vente au niveau service : une ligne de facture construite depuis un article du
/// catalogue, et l'emission qui sort du stock les seules lignes suivies - dans la meme
/// transaction que le numero, refusee si le stock ne couvre pas, sans jamais ressortir deux fois.
/// </summary>
public sealed class SalesInvoicingTests
{
    [Fact]
    public async Task Une_ligne_d_article_reprend_prix_et_tva_du_catalogue_et_se_surcharge_ligne_a_ligne()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var created = await harness.Billing.CreateInvoiceAsync(
            new CreateInvoiceRequest(
                SalesHarness.CustomerCode,
                SalesHarness.UnitCode,
                SalesHarness.Today,
                new[]
                {
                    // Tout vient du catalogue : designation, prix, TVA.
                    new InvoiceLineRequest(null, 2m, ArticleCode: "coca"),
                    // Prix negocie et libelle precise : le code article reste, le reste est surcharge.
                    new InvoiceLineRequest("Acces spa 2h", 1m, UnitPrice: 2_500m, ArticleCode: SalesHarness.SpaArticle),
                    // Ligne libre, forme historique.
                    new InvoiceLineRequest("Taxe de sejour", 2m, 150m, 0m)
                }),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(created);

        var lines = created.Value!.Lines.OrderBy(line => line.LineNumber).ToArray();

        Assert.Equal("Coca-Cola 33cl", lines[0].Designation);
        Assert.Equal(SalesHarness.CocaPrice, lines[0].UnitPrice);
        Assert.Equal(19m, lines[0].VatRate);
        Assert.Equal("COCA", lines[0].ArticleCode);

        Assert.Equal("Acces spa 2h", lines[1].Designation);
        Assert.Equal(2_500m, lines[1].UnitPrice);
        Assert.Equal(9m, lines[1].VatRate);
        Assert.Equal("SPA", lines[1].ArticleCode);

        Assert.Null(lines[2].ArticleCode);
        Assert.Equal(300m, lines[2].LineTotalExclVat);

        // 2 x 250 + 2 500 + 300 = 3 300 HT ; TVA 95 + 225 + 0 = 320.
        Assert.Equal(3_300m, created.Value.TotalExclVat);
        Assert.Equal(320m, created.Value.TotalVat);
    }

    [Fact]
    public async Task Un_article_inconnu_ou_inactif_et_une_ligne_libre_sans_prix_sont_refuses()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var unknown = await CreateAsync(harness, new InvoiceLineRequest(null, 1m, ArticleCode: "PAS-AU-CATALOGUE"));
        Assert.Equal(ApplicationErrorType.Validation, unknown.ErrorType);
        Assert.Contains("PAS-AU-CATALOGUE", unknown.Error);

        var deactivated = await harness.Catalog.SetArticleActiveAsync(SalesHarness.WaterArticle, false, SalesHarness.Context, CancellationToken.None);
        SalesHarness.AssertSucceeded(deactivated);

        var inactive = await CreateAsync(harness, new InvoiceLineRequest(null, 1m, ArticleCode: SalesHarness.WaterArticle));
        Assert.Equal(ApplicationErrorType.Validation, inactive.ErrorType);
        Assert.Contains("inactif", inactive.Error);

        var freeWithoutPrice = await CreateAsync(harness, new InvoiceLineRequest("Prestation", 1m));
        Assert.Equal(ApplicationErrorType.Validation, freeWithoutPrice.ErrorType);
        Assert.Contains("prix unitaire", freeWithoutPrice.Error);
    }

    [Fact]
    public async Task L_emission_sort_du_stock_les_seules_lignes_suivies_dans_le_magasin_indique()
    {
        await using var harness = await SalesHarness.CreateAsync();

        await harness.EnterStockAsync(SalesHarness.CocaStockItem, 10m, 80.00m);

        var draft = await CreateAsync(
            harness,
            new InvoiceLineRequest(null, 3m, ArticleCode: SalesHarness.CocaArticle),
            new InvoiceLineRequest(null, 1m, ArticleCode: SalesHarness.SpaArticle),
            new InvoiceLineRequest("Taxe de sejour", 1m, 150m, 0m));

        SalesHarness.AssertSucceeded(draft);

        // Sans magasin, une facture qui porte une ligne suivie ne s'emet pas - et reste intacte.
        var withoutWarehouse = await harness.Billing.IssueInvoiceAsync(
            draft.Value!.Id, null, SalesHarness.Context, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, withoutWarehouse.ErrorType);
        Assert.Contains("magasin", withoutWarehouse.Error);
        Assert.Equal(10m, await harness.StockAsync(SalesHarness.CocaStockItem));

        var issued = await harness.Billing.IssueInvoiceAsync(
            draft.Value.Id,
            new IssueInvoiceRequest(" mag-vte "),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(issued);
        Assert.Equal(InvoiceStatus.Issued, issued.Value!.Status);
        Assert.NotNull(issued.Value.Number);

        // Seule la ligne suivie est sortie : 10 - 3 = 7. Le spa et la taxe ne touchent pas le stock.
        Assert.Equal(7m, await harness.StockAsync(SalesHarness.CocaStockItem));

        var movement = Assert.Single(await harness.SaleMovementsAsync());
        Assert.Equal(StockMovementKind.Sale, movement.Kind);
        Assert.Equal(SalesHarness.WarehouseCode, movement.WarehouseCode);
        Assert.Equal(SalesHarness.CocaStockItem, movement.ItemCode);
        Assert.Equal(3m, movement.Quantity);
        Assert.Equal(-3m, movement.SignedQuantity);

        // La reference du mouvement est le numero de la facture : de la facture au registre et retour.
        Assert.Equal(issued.Value.Number, movement.Reference);

        // Valorisee au cout moyen pondere connu a l'instant de la vente.
        Assert.Equal(80.00m, movement.UnitCost);
    }

    [Fact]
    public async Task Un_stock_insuffisant_refuse_l_emission_et_ne_consomme_aucun_numero()
    {
        await using var harness = await SalesHarness.CreateAsync();

        await harness.EnterStockAsync(SalesHarness.CocaStockItem, 2m, 80.00m);

        var draft = await CreateAsync(harness, new InvoiceLineRequest(null, 3m, ArticleCode: SalesHarness.CocaArticle));
        SalesHarness.AssertSucceeded(draft);

        var refused = await harness.Billing.IssueInvoiceAsync(
            draft.Value!.Id,
            new IssueInvoiceRequest(SalesHarness.WarehouseCode),
            SalesHarness.Context,
            CancellationToken.None);

        // Le refus est celui du module Stocks, rendu tel quel : il nomme l'article et le disponible.
        Assert.False(refused.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, refused.ErrorType);
        Assert.Contains(SalesHarness.CocaStockItem, refused.Error);
        Assert.Contains("2", refused.Error);

        // Rien n'a ete ecrit : ni mouvement, ni numero, et la facture est toujours un brouillon.
        harness.DbContext.ChangeTracker.Clear();

        var stored = await harness.DbContext.Set<Invoice>().AsNoTracking().SingleAsync(invoice => invoice.Id == draft.Value.Id);
        Assert.Equal(InvoiceStatus.Draft, stored.Status);
        Assert.Null(stored.Number);
        Assert.Null(stored.IssuedSequence);
        Assert.Empty(await harness.SaleMovementsAsync());
        Assert.Equal(2m, await harness.StockAsync(SalesHarness.CocaStockItem));

        // Une fois le stock reapprovisionne, la meme facture s'emet et prend le PREMIER numero de
        // l'annee : l'emission refusee n'en a brule aucun.
        await harness.EnterStockAsync(SalesHarness.CocaStockItem, 5m, 90.00m);

        var issued = await harness.Billing.IssueInvoiceAsync(
            draft.Value.Id,
            new IssueInvoiceRequest(SalesHarness.WarehouseCode),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(issued);
        Assert.Equal(Invoice.FormatNumber(DateTime.UtcNow.Year, 1), issued.Value!.Number);
        Assert.Equal(4m, await harness.StockAsync(SalesHarness.CocaStockItem));
    }

    [Fact]
    public async Task Reemettre_ou_rejouer_la_sortie_ne_ressort_pas_la_marchandise()
    {
        await using var harness = await SalesHarness.CreateAsync();

        await harness.EnterStockAsync(SalesHarness.CocaStockItem, 10m, 80.00m);

        var draft = await CreateAsync(harness, new InvoiceLineRequest(null, 4m, ArticleCode: SalesHarness.CocaArticle));
        SalesHarness.AssertSucceeded(draft);

        var issued = await harness.Billing.IssueInvoiceAsync(
            draft.Value!.Id, new IssueInvoiceRequest(SalesHarness.WarehouseCode), SalesHarness.Context, CancellationToken.None);

        SalesHarness.AssertSucceeded(issued);
        Assert.Equal(6m, await harness.StockAsync(SalesHarness.CocaStockItem));

        // Reemettre : le domaine refuse (seul un brouillon s'emet) et le stock ne bouge pas.
        var again = await harness.Billing.IssueInvoiceAsync(
            draft.Value.Id, new IssueInvoiceRequest(SalesHarness.WarehouseCode), SalesHarness.Context, CancellationToken.None);

        Assert.False(again.Succeeded);
        Assert.Equal(6m, await harness.StockAsync(SalesHarness.CocaStockItem));

        // Rejouer la sortie elle-meme sous la meme reference : le port repond "deja enregistree"
        // et n'ecrit rien - c'est la garantie qui tient meme si l'appelant a perdu la reponse.
        var replayed = await harness.Inventory.RegisterSaleAsync(
            new RegisterSaleRequest(
                SalesHarness.WarehouseCode,
                issued.Value!.Number!,
                new[] { new StockExitLine(SalesHarness.CocaStockItem, 4m) }),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(replayed);
        Assert.True(replayed.Value!.AlreadyRegistered);
        Assert.Equal(0, replayed.Value.MovementCount);
        Assert.Single(await harness.SaleMovementsAsync());
        Assert.Equal(6m, await harness.StockAsync(SalesHarness.CocaStockItem));
    }

    [Fact]
    public async Task Une_sortie_de_vente_ne_se_saisit_pas_a_la_main()
    {
        await using var harness = await SalesHarness.CreateAsync();

        await harness.EnterStockAsync(SalesHarness.CocaStockItem, 10m, 80.00m);

        var refused = await harness.Inventory.CreateMovementAsync(
            new CreateStockMovementRequest(
                SalesHarness.WarehouseCode,
                SalesHarness.CocaStockItem,
                SalesHarness.Today,
                StockMovementKind.Sale,
                1m,
                null,
                "MANUEL",
                LotNumber: null,
                ExpiryDate: null,
                Notes: null,
                AdjustmentIsIncrease: null),
            SalesHarness.Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("issue", refused.Error);
        Assert.Equal(10m, await harness.StockAsync(SalesHarness.CocaStockItem));
    }

    private static Task<ApplicationResult<InvoiceResponse>> CreateAsync(SalesHarness harness, params InvoiceLineRequest[] lines)
    {
        return harness.Billing.CreateInvoiceAsync(
            new CreateInvoiceRequest(SalesHarness.CustomerCode, SalesHarness.UnitCode, SalesHarness.Today, lines),
            SalesHarness.Context,
            CancellationToken.None);
    }
}
