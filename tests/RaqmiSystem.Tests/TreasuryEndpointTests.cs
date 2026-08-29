using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Treasury;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Treasury;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the treasury module (cash receipts and their summary).
/// Uses the cashier system role, which is seeded with the treasury.read and treasury.write
/// permissions, so the per-permission authorization policies registered from PermissionCatalog
/// are enforced for real.
/// </summary>
public sealed class TreasuryEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private readonly RaqmiApiFactory _factory;

    public TreasuryEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Confirmed_receipt_refuses_modification()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("TRSCONF", "Treasury Confirm Hotel");

        await _factory.CreateUserAsync(
            "treasury.cashier1",
            "treasury.cashier1@example.com",
            "Treasury Cashier One",
            Password,
            RoleCatalog.Cashier);

        using var client = await _factory.CreateAuthenticatedClientAsync("treasury.cashier1", Password);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/treasury/receipts",
            new CreateCashReceiptRequest(
                ReceiptDate: new DateOnly(2026, 8, 10),
                HotelUnitCode: hotelUnitCode,
                Method: PaymentMethod.Cash,
                Amount: 250m,
                Notes: "Front desk cash"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CashReceiptResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(ReceiptStatus.Draft, created!.Status);

        var confirmResponse = await client.PostAsync($"/api/v1/treasury/receipts/{created.Id}/confirm", content: null);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<CashReceiptResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(confirmed);
        Assert.Equal(ReceiptStatus.Confirmed, confirmed!.Status);
        Assert.False(confirmed.CanEdit);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/treasury/receipts/{created.Id}",
            new UpdateCashReceiptRequest(
                ReceiptDate: new DateOnly(2026, 8, 10),
                HotelUnitCode: hotelUnitCode,
                Method: PaymentMethod.Cash,
                Amount: 999m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Receipt_summary_totals_confirmed_amounts_per_payment_method_and_excludes_cancelled()
    {
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("TRSSUM", "Treasury Summary Hotel");

        await _factory.CreateUserAsync(
            "treasury.cashier2",
            "treasury.cashier2@example.com",
            "Treasury Cashier Two",
            Password,
            RoleCatalog.Cashier);

        using var client = await _factory.CreateAuthenticatedClientAsync("treasury.cashier2", Password);

        var accountResponse = await client.PostAsJsonAsync(
            "/api/v1/treasury/bank-accounts",
            new CreateBankAccountRequest("TRS-BANK", "Compte tresorerie", "BIAT", "TN5904018104003699999999"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);

        var receiptDate = new DateOnly(2026, 8, 12);

        // Four receipts confirmed: only these must enter the default summary.
        foreach (var request in new[]
        {
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Cash, 100m),
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Cash, 50m),
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Card, 70m, BankAccountCode: "TRS-BANK"),
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Cheque, 30m, Reference: "CHQ-777", BankAccountCode: "TRS-BANK")
        })
        {
            var response = await client.PostAsJsonAsync("/api/v1/treasury/receipts", request, RaqmiApiFactory.JsonOptions);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<CashReceiptResponse>(RaqmiApiFactory.JsonOptions);
            Assert.NotNull(created);

            var confirmResponse = await client.PostAsync($"/api/v1/treasury/receipts/{created!.Id}/confirm", content: null);
            Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        }

        // One receipt left as a Draft: not real collected money yet.
        var draftResponse = await client.PostAsJsonAsync(
            "/api/v1/treasury/receipts",
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Cash, 500m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);

        // One receipt cancelled: it must not enter the total either.
        var cancelledCreateResponse = await client.PostAsJsonAsync(
            "/api/v1/treasury/receipts",
            new CreateCashReceiptRequest(receiptDate, hotelUnitCode, PaymentMethod.Cash, 999m),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, cancelledCreateResponse.StatusCode);

        var cancelledReceipt = await cancelledCreateResponse.Content.ReadFromJsonAsync<CashReceiptResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(cancelledReceipt);

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/treasury/receipts/{cancelledReceipt!.Id}/cancel",
            new CancelCashReceiptRequest("Erreur de saisie."),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // No explicit status filter: the summary only counts Confirmed receipts.
        var summaryResponse = await client.GetAsync(
            $"/api/v1/treasury/receipts/summary?from=2026-08-12&to=2026-08-12&hotelUnitCode={hotelUnitCode}");

        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        var summary = await summaryResponse.Content.ReadFromJsonAsync<CashReceiptSummaryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(ReceiptStatus.Confirmed, summary!.Status);
        Assert.Equal(4, summary.TotalCount);
        Assert.Equal(150m, summary.CashTotal);
        Assert.Equal(70m, summary.CardTotal);
        Assert.Equal(30m, summary.ChequeTotal);
        Assert.Equal(0m, summary.BankTransferTotal);
        Assert.Equal(250m, summary.GrandTotal);

        // An explicit status filter is honoured as-is: draft receipts remain inspectable.
        var draftSummaryResponse = await client.GetAsync(
            $"/api/v1/treasury/receipts/summary?from=2026-08-12&to=2026-08-12&hotelUnitCode={hotelUnitCode}&status=Draft");

        Assert.Equal(HttpStatusCode.OK, draftSummaryResponse.StatusCode);

        var draftSummary = await draftSummaryResponse.Content.ReadFromJsonAsync<CashReceiptSummaryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draftSummary);
        Assert.Equal(1, draftSummary!.TotalCount);
        Assert.Equal(500m, draftSummary.GrandTotal);
    }

    [Fact]
    public async Task Reading_receipts_without_the_treasury_read_permission_returns_403()
    {
        await _factory.CreateUserAsync(
            "treasury.reader",
            "treasury.reader@example.com",
            "Treasury Reader",
            Password,
            RoleCatalog.Reader); // reader has no treasury.* permission

        using var client = await _factory.CreateAuthenticatedClientAsync("treasury.reader", Password);

        var response = await client.GetAsync("/api/v1/treasury/receipts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
