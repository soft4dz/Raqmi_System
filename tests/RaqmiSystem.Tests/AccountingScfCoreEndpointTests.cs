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
}
