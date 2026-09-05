using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Common;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le reglement d'une facture cree un encaissement REEL en tresorerie - exactement un, confirme,
/// du montant TTC, lie a la facture par sa reference - et un reglement rejoue ne le double pas.
/// Sans mode de paiement, le comportement historique est conserve et signale.
/// </summary>
public sealed class SalesPaymentTests
{
    [Fact]
    public async Task Payer_cree_exactement_un_encaissement_confirme_et_rejouer_ne_le_double_pas()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var issued = await IssueAsync(harness);

        var paid = await harness.Billing.PayInvoiceAsync(
            issued.Id,
            new PayInvoiceRequest(PaymentMethod.Cash),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(paid);
        Assert.Equal(InvoiceStatus.Paid, paid.Value!.Invoice.Status);
        Assert.Null(paid.Value.Notice);

        var receipt = paid.Value.Receipt;
        Assert.NotNull(receipt);
        Assert.Equal(ReceiptStatus.Confirmed, receipt!.Status);
        Assert.Equal(PaymentMethod.Cash, receipt.Method);
        Assert.Equal(issued.TotalInclVat, receipt.Amount);
        Assert.Equal(SalesHarness.UnitCode, receipt.HotelUnitCode);

        // Le lien va dans les deux sens : la facture porte l'encaissement, l'encaissement
        // porte le numero de la facture.
        Assert.Equal(receipt.Id, paid.Value.Invoice.CashReceiptId);
        Assert.Equal(issued.Number, receipt.Reference);
        Assert.Contains(issued.Number!, receipt.Notes);

        // Rejouer : refus explicite, et toujours UN seul encaissement en base.
        var replayed = await harness.Billing.PayInvoiceAsync(
            issued.Id,
            new PayInvoiceRequest(PaymentMethod.Cash),
            SalesHarness.Context,
            CancellationToken.None);

        Assert.False(replayed.Succeeded);
        Assert.Equal(ApplicationErrorType.Conflict, replayed.ErrorType);

        harness.DbContext.ChangeTracker.Clear();

        Assert.Equal(1, await harness.DbContext.Set<CashReceipt>().CountAsync());

        var stored = await harness.DbContext.Set<Invoice>().AsNoTracking().SingleAsync(invoice => invoice.Id == issued.Id);
        Assert.Equal(InvoiceStatus.Paid, stored.Status);
        Assert.Equal(receipt.Id, stored.CashReceiptId);
    }

    [Fact]
    public async Task Un_reglement_par_cheque_porte_la_piece_du_paiement_et_nomme_la_facture()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var issued = await IssueAsync(harness);

        // Sans compte bancaire, la tresorerie refuse un cheque : le refus est rendu tel quel et
        // la facture reste emise, sans encaissement.
        var refused = await harness.Billing.PayInvoiceAsync(
            issued.Id,
            new PayInvoiceRequest(PaymentMethod.Cheque, Reference: "CHQ-4471"),
            SalesHarness.Context,
            CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Contains("bank account", refused.Error);

        harness.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, await harness.DbContext.Set<CashReceipt>().CountAsync());
        Assert.Equal(InvoiceStatus.Issued, (await harness.DbContext.Set<Invoice>().AsNoTracking().SingleAsync(i => i.Id == issued.Id)).Status);

        var paid = await harness.Billing.PayInvoiceAsync(
            issued.Id,
            new PayInvoiceRequest(PaymentMethod.Cheque, SalesHarness.BankAccountCode, "CHQ-4471", Notes: "Remis en banque le jour meme."),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(paid);

        var receipt = paid.Value!.Receipt!;
        Assert.Equal("CHQ-4471", receipt.Reference);
        Assert.Equal(SalesHarness.BankAccountCode, receipt.BankAccountCode);
        Assert.Contains(issued.Number!, receipt.Notes);
        Assert.Contains("Remis en banque", receipt.Notes);
    }

    [Fact]
    public async Task Sans_mode_de_paiement_la_facture_est_marquee_payee_et_l_absence_d_encaissement_est_signalee()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var issued = await IssueAsync(harness);

        var marked = await harness.Billing.PayInvoiceAsync(issued.Id, null, SalesHarness.Context, CancellationToken.None);

        SalesHarness.AssertSucceeded(marked);
        Assert.Equal(InvoiceStatus.Paid, marked.Value!.Invoice.Status);
        Assert.Null(marked.Value.Receipt);
        Assert.Null(marked.Value.Invoice.CashReceiptId);
        Assert.Contains("sans encaissement", marked.Value.Notice);

        harness.DbContext.ChangeTracker.Clear();
        Assert.Equal(0, await harness.DbContext.Set<CashReceipt>().CountAsync());

        // Une facture payee - meme sans encaissement - ne se regle plus : pas d'encaissement
        // tardif qui doublerait un paiement deja constate ailleurs.
        var late = await harness.Billing.PayInvoiceAsync(
            issued.Id, new PayInvoiceRequest(PaymentMethod.Cash), SalesHarness.Context, CancellationToken.None);

        Assert.False(late.Succeeded);
        Assert.Equal(0, await harness.DbContext.Set<CashReceipt>().CountAsync());
    }

    [Fact]
    public async Task Un_brouillon_ne_se_regle_pas()
    {
        await using var harness = await SalesHarness.CreateAsync();

        var draft = await harness.Billing.CreateInvoiceAsync(
            new CreateInvoiceRequest(
                SalesHarness.CustomerCode,
                SalesHarness.UnitCode,
                SalesHarness.Today,
                new[] { new InvoiceLineRequest(null, 1m, ArticleCode: SalesHarness.SpaArticle) }),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(draft);

        var refused = await harness.Billing.PayInvoiceAsync(
            draft.Value!.Id, new PayInvoiceRequest(PaymentMethod.Cash), SalesHarness.Context, CancellationToken.None);

        Assert.Equal(ApplicationErrorType.Validation, refused.ErrorType);
        Assert.Equal(0, await harness.DbContext.Set<CashReceipt>().CountAsync());
    }

    private static async Task<InvoiceResponse> IssueAsync(SalesHarness harness)
    {
        var draft = await harness.Billing.CreateInvoiceAsync(
            new CreateInvoiceRequest(
                SalesHarness.CustomerCode,
                SalesHarness.UnitCode,
                SalesHarness.Today,
                new[] { new InvoiceLineRequest(null, 2m, ArticleCode: SalesHarness.SpaArticle) }),
            SalesHarness.Context,
            CancellationToken.None);

        SalesHarness.AssertSucceeded(draft);

        var issued = await harness.Billing.IssueInvoiceAsync(draft.Value!.Id, null, SalesHarness.Context, CancellationToken.None);

        SalesHarness.AssertSucceeded(issued);

        return issued.Value!;
    }
}
