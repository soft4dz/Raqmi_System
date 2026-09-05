using System.Net;
using System.Net.Http.Json;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

public sealed class AccountingScfCoreEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private readonly RaqmiApiFactory factory;
    public AccountingScfCoreEndpointTests(RaqmiApiFactory factory)=>this.factory=factory;

    [Fact]
    public async Task Fiscal_period_numbering_and_closing_are_enforced_end_to_end()
    {
        const string password="Strong-Test-Password-2026!";
        await factory.CreateUserAsync("scf.admin","scf.admin@example.test","SCF admin",password,RoleCatalog.SystemAdministrator);
        var client=await factory.CreateAuthenticatedClientAsync("scf.admin",password);

        var yearResponse=await client.PostAsJsonAsync("/api/v1/accounting/fiscal-years",new CreateFiscalYearRequest("FY27",new DateOnly(2027,1,1),new DateOnly(2027,12,31)),RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK,yearResponse.StatusCode);
        var year=await yearResponse.Content.ReadFromJsonAsync<FiscalYearResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(year);

        var seed=await client.PostAsync("/api/v1/accounting/scf/seed",null);
        Assert.Equal(HttpStatusCode.OK,seed.StatusCode);
        var journal=await client.PostAsJsonAsync("/api/v1/accounting/journals",new CreateAccountingJournalRequest("V27","Ventes 2027"),RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created,journal.StatusCode);
        var partyResponse=await client.PostAsJsonAsync("/api/v1/accounting/parties",new CreatePartyRequest("CLI27","Client 2027",PartyKind.Customer),RaqmiApiFactory.JsonOptions);
        var party=await partyResponse.Content.ReadFromJsonAsync<PartyResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(party);

        async Task<JournalEntryResponse> CreateAndPostAsync(string reference,bool payment)
        {
            var lines=payment
                ? new JournalEntryLineRequest[]{new("512000","Banque",100m,0m),new("411000","Client",0m,100m,party.Id)}
                : [new("411000","Client",100m,0m,party.Id),new("706000","Vente",0m,100m)];
            var created=await client.PostAsJsonAsync("/api/v1/accounting/entries",new CreateJournalEntryRequest(new DateOnly(2027,1,15),"V27",reference,reference,lines),RaqmiApiFactory.JsonOptions);
            Assert.Equal(HttpStatusCode.Created,created.StatusCode);
            var draft=await created.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
            Assert.NotNull(draft);
            var posted=await client.PostAsync($"/api/v1/accounting/entries/{draft.Id}/post",null);
            Assert.Equal(HttpStatusCode.OK,posted.StatusCode);
            return (await posted.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions))!;
        }

        var first=await CreateAndPostAsync("F-1",false);
        var second=await CreateAndPostAsync("R-1",true);
        Assert.Equal("V27-FY27-000001",first.DocumentNumber);
        Assert.Equal("V27-FY27-000002",second.DocumentNumber);

        var debitLine=first.Lines.Single(x=>x.PartyId==party.Id);
        var creditLine=second.Lines.Single(x=>x.PartyId==party.Id);
        var reconciliation=await client.PostAsJsonAsync("/api/v1/accounting/reconciliations",new CreateReconciliationRequest("L27",party.Id,[new(debitLine.Id,100m)],[new(creditLine.Id,100m)]),RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK,reconciliation.StatusCode);
        var auxiliary=await client.GetFromJsonAsync<AuxiliaryBalanceRow[]>("/api/v1/accounting/auxiliary-balance",RaqmiApiFactory.JsonOptions);
        var partyBalance=Assert.Single(auxiliary!,x=>x.PartyId==party.Id);
        Assert.Equal(0m,partyBalance.Balance);
        Assert.Equal(0m,partyBalance.Outstanding);

        var periods=await client.GetFromJsonAsync<AccountingPeriodResponse[]>($"/api/v1/accounting/fiscal-years/{year.Id}/periods",RaqmiApiFactory.JsonOptions);
        var january=Assert.Single(periods!,x=>x.Number==1);
        var close=await client.PostAsync($"/api/v1/accounting/periods/{january.Id}/close",null);
        Assert.Equal(HttpStatusCode.OK,close.StatusCode);

        var refused=await client.PostAsJsonAsync("/api/v1/accounting/entries",new CreateJournalEntryRequest(new DateOnly(2027,1,20),"V27","Late",null,[new("411000","Client",1m,0m),new("706000","Vente",0m,1m)]),RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict,refused.StatusCode);

        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var actions=await db.AuditLogs.AsNoTracking().Where(x=>x.Action.StartsWith("accounting.")).Select(x=>x.Action).ToArrayAsync();
        Assert.Contains("accounting.fiscal_year.created",actions);
        Assert.Contains("accounting.period.closed",actions);
        Assert.Contains("accounting.reconciliation.created",actions);
        Assert.Contains("accounting.journal_entry.posted",actions);
    }

    /// <summary>
    /// B7. Un client a 1 500 de factures et 1 000 d'encaissement, lettres a hauteur de 1 000 : il
    /// doit 500 et la balance auxiliaire doit le dire. L'ancienne version sommait les allocations
    /// des DEUX cotes (2 000 de « lettre ») puis calculait max(0, |solde| - lettre) = 0 :
    /// « rien a recouvrer », sur le seul rapport de recouvrement du produit.
    /// </summary>
    [Fact]
    public async Task Auxiliary_balance_shows_what_a_customer_still_owes_after_a_partial_lettering()
    {
        var client = await CreateAdminClientAsync("scf.b7.customer");
        await SeedAsync(client);
        await CreateFiscalYearAsync(client, "FY25", 2025);
        await CreateJournalAsync(client, "VB7C", "Ventes B7");
        var party = await CreatePartyAsync(client, "CLI-B7", "Client B7", PartyKind.Customer);

        var invoice = await CreateAndPostAsync(client, new DateOnly(2025, 3, 10), "VB7C", "F-B7-1",
            [new("411000", "Client", 1_500m, 0m, party.Id), new("706000", "Vente", 0m, 1_500m)]);
        var payment = await CreateAndPostAsync(client, new DateOnly(2025, 3, 20), "VB7C", "R-B7-1",
            [new("512000", "Banque", 1_000m, 0m), new("411000", "Client", 0m, 1_000m, party.Id)]);

        var invoiceLine = invoice.Lines.Single(x => x.PartyId == party.Id);
        var paymentLine = payment.Lines.Single(x => x.PartyId == party.Id);

        var reconciliation = await client.PostAsJsonAsync(
            "/api/v1/accounting/reconciliations",
            new CreateReconciliationRequest("L-B7C", party.Id, [new(invoiceLine.Id, 1_000m)], [new(paymentLine.Id, 1_000m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, reconciliation.StatusCode);

        var row = await GetAuxiliaryRowAsync(client, "?kind=Customer", party.Id);
        Assert.Equal(1_500m, row.Debit);
        Assert.Equal(1_000m, row.Credit);
        Assert.Equal(500m, row.Balance);
        Assert.Equal(1_000m, row.Reconciled);
        Assert.Equal(500m, row.Outstanding);

        // Meme lecture sur la fenetre de l'exercice : les allocations suivent la fenetre des mouvements.
        var windowed = await GetAuxiliaryRowAsync(client, "?from=2025-01-01&to=2025-12-31", party.Id);
        Assert.Equal(1_000m, windowed.Reconciled);
        Assert.Equal(500m, windowed.Outstanding);

        // Fenetre arretee AVANT l'encaissement : a cette date, la facture etait entierement ouverte.
        var before = await GetAuxiliaryRowAsync(client, "?from=2025-01-01&to=2025-03-15", party.Id);
        Assert.Equal(0m, before.Reconciled);
        Assert.Equal(1_500m, before.Outstanding);
    }

    /// <summary>
    /// B7, cote fournisseur : le cote naturel est le credit. 1 500 de factures recues, 1 000 de
    /// reglement lettre : il reste 500 a payer.
    /// </summary>
    [Fact]
    public async Task Auxiliary_balance_shows_what_is_still_owed_to_a_supplier_after_a_partial_lettering()
    {
        var client = await CreateAdminClientAsync("scf.b7.supplier");
        await SeedAsync(client);
        await CreateFiscalYearAsync(client, "FY24", 2024);
        await CreateJournalAsync(client, "AB7F", "Achats B7");
        var party = await CreatePartyAsync(client, "FOU-B7", "Fournisseur B7", PartyKind.Supplier);

        var purchase = await CreateAndPostAsync(client, new DateOnly(2024, 5, 3), "AB7F", "FA-B7-1",
            [new("607000", "Achats", 1_500m, 0m), new("401000", "Fournisseur", 0m, 1_500m, party.Id)]);
        var payment = await CreateAndPostAsync(client, new DateOnly(2024, 5, 28), "AB7F", "RG-B7-1",
            [new("401000", "Fournisseur", 1_000m, 0m, party.Id), new("512000", "Banque", 0m, 1_000m)]);

        var purchaseLine = purchase.Lines.Single(x => x.PartyId == party.Id);
        var paymentLine = payment.Lines.Single(x => x.PartyId == party.Id);

        var reconciliation = await client.PostAsJsonAsync(
            "/api/v1/accounting/reconciliations",
            new CreateReconciliationRequest("L-B7F", party.Id, [new(paymentLine.Id, 1_000m)], [new(purchaseLine.Id, 1_000m)]),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, reconciliation.StatusCode);

        var row = await GetAuxiliaryRowAsync(client, "?kind=Supplier", party.Id);
        Assert.Equal(1_000m, row.Debit);
        Assert.Equal(1_500m, row.Credit);
        Assert.Equal(-500m, row.Balance);
        Assert.Equal(1_000m, row.Reconciled);
        Assert.Equal(500m, row.Outstanding);
    }

    /// <summary>
    /// B6. Comptabiliser hors de toute periode ouverte est refuse - y compris quand l'exercice
    /// existe sans periodes (CreateMonthlyPeriods=false), cas qui laissait auparavant l'ecriture
    /// entrer dans les livres SANS numero de piece, definitivement. Et la periode manquante se
    /// cree par l'API, plus par un INSERT en production.
    /// </summary>
    [Fact]
    public async Task Posting_is_refused_outside_any_open_period_until_the_period_is_created()
    {
        var client = await CreateAdminClientAsync("scf.b6.periods");
        await SeedAsync(client);
        var year = await CreateFiscalYearAsync(client, "FY30", 2030, createMonthlyPeriods: false);
        await CreateJournalAsync(client, "VB6", "Ventes B6");

        // Le brouillon est insere directement : la saisie par l'API refuserait deja la date, et
        // c'est la COMPTABILISATION qui est en cause ici.
        Guid draftId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
            var draft = new JournalEntry(new DateOnly(2030, 3, 15), "VB6", "Hors periode", "HP-1");
            draft.ReplaceLines([new("411000", "Client", 50m, 0m), new("706000", "Vente", 0m, 50m)]);
            draft.MarkCreated("scf.b6.periods", DateTimeOffset.UtcNow);
            db.Set<JournalEntry>().Add(draft);
            await db.SaveChangesAsync();
            draftId = draft.Id;
        }

        var refused = await client.PostAsync($"/api/v1/accounting/entries/{draftId}/post", null);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("period", await refused.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var created = await PostPeriodAsync(client, year.Id, 3, new DateOnly(2030, 3, 1), new DateOnly(2030, 3, 31));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var period = await created.Content.ReadFromJsonAsync<AccountingPeriodResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(period);
        Assert.Equal(3, period.Number);
        Assert.Equal(year.Id, period.FiscalYearId);
        Assert.Equal(AccountingPeriodStatus.Open, period.Status);

        // Garde-fous de l'ouverture : numero deja pris, chevauchement, hors exercice, dates
        // inversees, exercice inconnu.
        Assert.Equal(HttpStatusCode.Conflict, (await PostPeriodAsync(client, year.Id, 3, new DateOnly(2030, 5, 1), new DateOnly(2030, 5, 31))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostPeriodAsync(client, year.Id, 4, new DateOnly(2030, 3, 15), new DateOnly(2030, 4, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostPeriodAsync(client, year.Id, 5, new DateOnly(2031, 1, 1), new DateOnly(2031, 1, 31))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostPeriodAsync(client, year.Id, 6, new DateOnly(2030, 6, 30), new DateOnly(2030, 6, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostPeriodAsync(client, Guid.NewGuid(), 1, new DateOnly(2030, 7, 1), new DateOnly(2030, 7, 31))).StatusCode);

        var posted = await client.PostAsync($"/api/v1/accounting/entries/{draftId}/post", null);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var entry = await posted.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(entry);
        Assert.Equal("VB6-FY30-000001", entry.DocumentNumber);
        Assert.Equal(year.Id, entry.FiscalYearId);

        using var auditScope = factory.Services.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        Assert.True(await auditDb.AuditLogs.AsNoTracking().AnyAsync(x => x.Action == "accounting.period.created"));
    }

    private async Task<HttpClient> CreateAdminClientAsync(string userName)
    {
        const string password = "Strong-Test-Password-2026!";
        await factory.CreateUserAsync(userName, $"{userName}@example.test", userName, password, RoleCatalog.SystemAdministrator);
        return await factory.CreateAuthenticatedClientAsync(userName, password);
    }

    private static async Task SeedAsync(HttpClient client)
    {
        var seed = await client.PostAsync("/api/v1/accounting/scf/seed", null);
        Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
    }

    private static async Task<FiscalYearResponse> CreateFiscalYearAsync(HttpClient client, string code, int year, bool createMonthlyPeriods = true)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/fiscal-years",
            new CreateFiscalYearRequest(code, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), createMonthlyPeriods),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FiscalYearResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task CreateJournalAsync(HttpClient client, string code, string label)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/journals",
            new CreateAccountingJournalRequest(code, label),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<PartyResponse> CreatePartyAsync(HttpClient client, string code, string name, PartyKind kind)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/parties",
            new CreatePartyRequest(code, name, kind),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static async Task<JournalEntryResponse> CreateAndPostAsync(
        HttpClient client,
        DateOnly date,
        string journalCode,
        string reference,
        JournalEntryLineRequest[] lines)
    {
        var created = await client.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(date, journalCode, reference, reference, lines),
            RaqmiApiFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        var posted = await client.PostAsync($"/api/v1/accounting/entries/{draft.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions))!;
    }

    private static Task<HttpResponseMessage> PostPeriodAsync(HttpClient client, Guid fiscalYearId, int number, DateOnly startsOn, DateOnly endsOn)
    {
        return client.PostAsJsonAsync(
            $"/api/v1/accounting/fiscal-years/{fiscalYearId}/periods",
            new CreateAccountingPeriodRequest(number, startsOn, endsOn),
            RaqmiApiFactory.JsonOptions);
    }

    private static async Task<AuxiliaryBalanceRow> GetAuxiliaryRowAsync(HttpClient client, string query, Guid partyId)
    {
        var rows = await client.GetFromJsonAsync<AuxiliaryBalanceRow[]>("/api/v1/accounting/auxiliary-balance" + query, RaqmiApiFactory.JsonOptions);
        return Assert.Single(rows!, x => x.PartyId == partyId);
    }
}
