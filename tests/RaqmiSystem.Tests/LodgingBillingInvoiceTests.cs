using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Application.Lodging;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Infrastructure.Audit;
using RaqmiSystem.Infrastructure.Lodging;

namespace RaqmiSystem.Tests;

/// <summary>
/// Du folio a la facture (A4) : la facture est construite par le module Facturation depuis les
/// prestations du folio, montants et TVA identiques, rattachee au folio ; un folio ne se facture
/// qu'une fois, un folio vide ne se facture pas, et facturer ne solde pas le depart.
/// </summary>
public sealed class LodgingBillingInvoiceTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task La_facture_du_folio_reprend_ses_prestations_a_l_identique_et_s_y_rattache()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);
        var invoicing = CreateInvoicing(harness);

        var stay = await harness.BookAsync(Today, Today.AddDays(2), harness.StandardRooms[0].Id);
        Assert.True(stay.Succeeded, stay.Error);

        var checkedIn = await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(checkedIn.Succeeded, checkedIn.Error);

        // Deux extras a 19 % - 3 boissons a 357 TTC (HT unitaire rond) et 3 eaux pour 1 500 TTC
        // (HT unitaire non representable au centime) - et une taxe a 0 %, en plus de la nuitee.
        var minibar = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(Today, "Minibar - Coca-Cola", 1_071m, ChargeKind.Extra, Quantity: 3m, VatRate: 19m),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(minibar.Succeeded, minibar.Error);

        var water = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(Today, "Minibar - Eau", 1_500m, ChargeKind.Extra, Quantity: 3m, VatRate: 19m),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(water.Succeeded, water.Error);

        var tax = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(Today, "Taxe de sejour", 200m, ChargeKind.Tax, VatRate: 0m),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(tax.Succeeded, tax.Error);

        var folio = (await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None)).Value!;

        var invoiced = await invoicing.InvoiceFolioAsync(stay.Value.Id, folio.Id, PmsHarness.Context, CancellationToken.None);

        Assert.True(invoiced.Succeeded, invoiced.Error);

        var invoice = invoiced.Value!.Invoice;
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(PmsHarness.CustomerCode, invoice.CustomerCode);
        Assert.Equal(PmsHarness.UnitCode, invoice.HotelUnitCode);
        Assert.Equal(folio.Number, invoiced.Value.FolioNumber);

        // MONTANTS ET TVA IDENTIQUES AU FOLIO : le TTC de la facture est le total des prestations du
        // folio, et chaque ligne recalcule exactement le HT et la TVA que le folio portait.
        Assert.Equal(folio.TotalCharges, invoice.TotalInclVat);
        Assert.Equal(folio.TotalCharges, invoiced.Value.FolioTotalCharges);

        var lines = invoice.Lines.OrderBy(line => line.LineNumber).ToArray();
        var charges = folio.Charges.Where(charge => charge.Kind != ChargeKind.Settlement).OrderBy(charge => charge.LineNumber).ToArray();

        Assert.Equal(charges.Length, lines.Length);

        for (var index = 0; index < charges.Length; index++)
        {
            Assert.Equal(charges[index].AmountExclVat, lines[index].LineTotalExclVat);
            Assert.Equal(charges[index].VatAmount, lines[index].VatAmount);
            Assert.Equal(charges[index].Amount, lines[index].LineTotalInclVat);
            Assert.Equal(charges[index].VatRate ?? 0m, lines[index].VatRate);
        }

        // 1 071 TTC pour 3 a 19 % : 900 HT, soit 300 l'unite - la quantite est conservee.
        var minibarLine = Assert.Single(lines, line => line.Designation == "Minibar - Coca-Cola");
        Assert.Equal(3m, minibarLine.Quantity);
        Assert.Equal(300m, minibarLine.UnitPrice);
        Assert.Equal(900m, minibarLine.LineTotalExclVat);
        Assert.Equal(171m, minibarLine.VatAmount);

        // 1 500 TTC pour 3 : 1 260,50 HT, que 3 ne divise pas au centime - la ligne passe en
        // quantite 1 avec la quantite rappelee dans le libelle, et les montants restent exacts.
        var waterLine = Assert.Single(lines, line => line.Designation == "Minibar - Eau (x3)");
        Assert.Equal(1m, waterLine.Quantity);
        Assert.Equal(1_260.50m, waterLine.LineTotalExclVat);
        Assert.Equal(239.50m, waterLine.VatAmount);
        Assert.Equal(1_500m, waterLine.LineTotalInclVat);

        // Le folio porte sa facture...
        var reloaded = (await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None)).Value!;
        Assert.Equal(invoice.Id, reloaded.InvoiceId);

        // ...mais facturer ne solde pas : le depart exige toujours un solde nul.
        var departure = await harness.Service.CheckOutAsync(stay.Value.Id, PmsHarness.Context, CancellationToken.None);
        Assert.False(departure.Succeeded);
        Assert.Contains("ne sont pas soldes", departure.Error);
    }

    [Fact]
    public async Task Un_folio_ne_se_facture_qu_une_fois_et_un_folio_vide_ne_se_facture_pas()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);
        var invoicing = CreateInvoicing(harness);

        var stay = await harness.BookAsync(Today, Today.AddDays(1), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        var guestFolio = (await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None)).Value!;

        var first = await invoicing.InvoiceFolioAsync(stay.Value.Id, guestFolio.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(first.Succeeded, first.Error);

        var second = await invoicing.InvoiceFolioAsync(stay.Value.Id, guestFolio.Id, PmsHarness.Context, CancellationToken.None);
        Assert.False(second.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, second.ErrorType);
        Assert.Contains("deja ete facture", second.Error);

        // Une seule facture existe pour ce folio.
        Assert.Equal(1, await harness.DbContext.Set<Invoice>().CountAsync());

        // Un folio societe ouvert sans aucune ligne : rien a facturer.
        var companyFolio = await harness.Service.CreateFolioAsync(
            stay.Value.Id,
            new CreateFolioRequest(FolioKind.Company, PmsHarness.CustomerCode, "Prise en charge societe"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(companyFolio.Succeeded, companyFolio.Error);

        var empty = await invoicing.InvoiceFolioAsync(stay.Value.Id, companyFolio.Value!.Id, PmsHarness.Context, CancellationToken.None);
        Assert.False(empty.Succeeded);
        Assert.Equal(ApplicationErrorType.Validation, empty.ErrorType);
        Assert.Contains("aucune prestation", empty.Error);

        // Un folio d'un autre dossier n'est pas atteignable par ce dossier.
        var foreign = await invoicing.InvoiceFolioAsync(Guid.NewGuid(), guestFolio.Id, PmsHarness.Context, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.NotFound, foreign.ErrorType);
    }

    [Fact]
    public async Task Une_ligne_transferee_est_facturee_sur_le_folio_qui_l_a_recue_et_neutralisee_sur_l_autre()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);
        var invoicing = CreateInvoicing(harness);

        var stay = await harness.BookAsync(Today, Today.AddDays(2), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        var extra = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(Today, "Diner", 4_000m, ChargeKind.Extra, VatRate: 9m),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(extra.Succeeded, extra.Error);

        var companyFolio = await harness.Service.CreateFolioAsync(
            stay.Value.Id,
            new CreateFolioRequest(FolioKind.Company, PmsHarness.CustomerCode, "La societe prend la chambre"),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(companyFolio.Succeeded, companyFolio.Error);

        var guestFolio = (await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None)).Value!;
        var night = Assert.Single(guestFolio.Charges, charge => charge.Kind == ChargeKind.Night);

        var transferred = await harness.Service.TransferFolioChargeAsync(
            stay.Value.Id,
            new TransferFolioChargeRequest(night.Id, companyFolio.Value!.Id, "La societe prend la chambre."),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(transferred.Succeeded, transferred.Error);

        // Le folio client : la nuit et sa contre-passation se neutralisent, seul le diner reste.
        var guestInvoice = await invoicing.InvoiceFolioAsync(stay.Value.Id, guestFolio.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(guestInvoice.Succeeded, guestInvoice.Error);

        var guestLine = Assert.Single(guestInvoice.Value!.Invoice.Lines);
        Assert.Equal("Diner", guestLine.Designation);
        Assert.Equal(4_000m, guestLine.LineTotalInclVat);
        Assert.Equal(4_000m, guestInvoice.Value.Invoice.TotalInclVat);

        // Le folio societe : la nuit recue, facturee au payeur du folio.
        var companyInvoice = await invoicing.InvoiceFolioAsync(stay.Value.Id, companyFolio.Value.Id, PmsHarness.Context, CancellationToken.None);
        Assert.True(companyInvoice.Succeeded, companyInvoice.Error);

        var companyLine = Assert.Single(companyInvoice.Value!.Invoice.Lines);
        Assert.Contains("Nuitee", companyLine.Designation);
        Assert.Equal(PmsHarness.NightlyRate, companyLine.LineTotalInclVat);
        Assert.Equal(PmsHarness.NightlyRate, companyInvoice.Value.Invoice.TotalInclVat);
    }

    [Fact]
    public async Task Un_geste_commercial_negatif_bloque_la_facturation_avec_un_message_explicite()
    {
        await using var harness = await PmsHarness.CreateAsync(standardRooms: 1, suites: 0);
        var invoicing = CreateInvoicing(harness);

        var stay = await harness.BookAsync(Today, Today.AddDays(1), harness.StandardRooms[0].Id);
        await harness.Service.CheckInAsync(stay.Value!.Id, PmsHarness.Context, CancellationToken.None);

        var gesture = await harness.Service.AddFolioChargeAsync(
            stay.Value.Id,
            new AddFolioChargeRequest(Today, "Geste commercial", -1_000m, ChargeKind.Adjustment),
            PmsHarness.Context,
            CancellationToken.None);

        Assert.True(gesture.Succeeded, gesture.Error);

        var folio = (await harness.Service.GetFolioAsync(stay.Value.Id, CancellationToken.None)).Value!;

        // Une ligne de facture ne porte pas de montant negatif : plutot que de facturer plus que
        // le solde, la facturation refuse et dit quoi regulariser.
        var refused = await invoicing.InvoiceFolioAsync(stay.Value.Id, folio.Id, PmsHarness.Context, CancellationToken.None);
        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("Geste commercial", refused.Error);
        Assert.Equal(0, await harness.DbContext.Set<Invoice>().CountAsync());
    }

    private static FolioInvoicingService CreateInvoicing(PmsHarness harness)
    {
        var auditWriter = new AuditLogWriter(harness.DbContext);

        return new FolioInvoicingService(
            harness.DbContext,
            auditWriter,
            SalesTestServices.CreateBillingService(harness.DbContext, auditWriter));
    }
}
