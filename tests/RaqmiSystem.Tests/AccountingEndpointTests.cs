using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Accounting;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Accounting;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the SCF accounting module. Each test provisions its own
/// single-purpose role carrying exactly the accounting permission keys it needs (the keys are
/// seeded from PermissionCatalog by SecuritySeeder during factory startup), so the
/// per-permission authorization policies registered in Program.cs are enforced for real.
///
/// The tests share one in-memory database (one factory per test class), so each one works on its
/// own account codes, its own journal and its own calendar month, and every trial balance is
/// requested over an explicit date range. That keeps them independent of the order xunit runs
/// them in.
/// </summary>
public sealed class AccountingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string AccountingRead = "accounting.read";
    private const string AccountingWrite = "accounting.write";
    private const string AccountingPost = "accounting.post";

    private readonly RaqmiApiFactory _factory;

    public AccountingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task An_entry_goes_from_unbalanced_draft_to_posted_and_is_corrected_by_a_reversal()
    {
        await CreateAccountingUserAsync(
            "accounting.writer",
            "accounting.writer@example.com",
            "Accounting Writer",
            AccountingRead, AccountingWrite);

        await CreateAccountingUserAsync(
            "accounting.poster",
            "accounting.poster@example.com",
            "Accounting Poster",
            AccountingRead, AccountingPost);

        using var writerClient = await _factory.CreateAuthenticatedClientAsync("accounting.writer", Password);
        using var posterClient = await _factory.CreateAuthenticatedClientAsync("accounting.poster", Password);

        // A class-6 account (charges) cannot be declared as a revenue: the class/kind coherence
        // rule is enforced before anything is written.
        var incoherent = await writerClient.PostAsJsonAsync(
            "/api/v1/accounting/accounts",
            new CreateChartAccountRequest("606900", "Achats", AccountKind.Revenue),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, incoherent.StatusCode);

        var clients = await CreateAccountAsync(writerClient, "411100", "Clients", AccountKind.Asset);
        Assert.Equal(4, clients.AccountClass);
        Assert.Equal("Comptes de tiers", clients.AccountClassLabel);

        await CreateAccountAsync(writerClient, "706100", "Ventes de prestations hotelieres", AccountKind.Revenue);

        // Journal codes are normalized upper-case.
        var journalResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/accounting/journals",
            new CreateAccountingJournalRequest("vea", "Ventes"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, journalResponse.StatusCode);

        var journal = await journalResponse.Content.ReadFromJsonAsync<AccountingJournalResponse>(
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(journal);
        Assert.Equal("VEA", journal!.Code);

        // An unbalanced draft is perfectly legal - it is only posting that is gated.
        var createResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(
                EntryDate: new DateOnly(2026, 4, 15),
                JournalCode: "VEA",
                Label: "Facture 42 Sonatrach",
                Reference: "FAC-2026-000042",
                Lines: new[]
                {
                    new JournalEntryLineRequest("411100", "Client Sonatrach", 11_900.00m, 0m),
                    new JournalEntryLineRequest("706100", "Prestations hotelieres", 0m, 10_000.00m)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal(EntryStatus.Draft, draft!.Status);
        Assert.False(draft.IsBalanced);
        Assert.True(draft.CanEdit);

        // Capturing and posting are distinct acts: the writer holds accounting.write only.
        var forbiddenPost = await writerClient.PostAsync($"/api/v1/accounting/entries/{draft.Id}/post", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPost.StatusCode);

        // Even with the right permission, an unbalanced entry cannot enter the books.
        var unbalancedPost = await posterClient.PostAsync($"/api/v1/accounting/entries/{draft.Id}/post", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, unbalancedPost.StatusCode);
        Assert.Contains("balanced", await unbalancedPost.Content.ReadAsStringAsync());

        var fixedResponse = await writerClient.PutAsJsonAsync(
            $"/api/v1/accounting/entries/{draft.Id}/lines",
            new UpdateJournalEntryLinesRequest(new[]
            {
                new JournalEntryLineRequest("411100", "Client Sonatrach", 11_900.00m, 0m),
                new JournalEntryLineRequest("706100", "Prestations hotelieres", 0m, 11_900.00m)
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, fixedResponse.StatusCode);

        var balanced = await fixedResponse.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(balanced);
        Assert.True(balanced!.IsBalanced);

        var postResponse = await posterClient.PostAsync($"/api/v1/accounting/entries/{draft.Id}/post", content: null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var posted = await postResponse.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(posted);
        Assert.Equal(EntryStatus.Posted, posted!.Status);
        Assert.False(posted.CanEdit);
        Assert.NotNull(posted.PostedAt);

        // Immutability: a posted entry rejects any change to its lines.
        var immutable = await writerClient.PutAsJsonAsync(
            $"/api/v1/accounting/entries/{draft.Id}/lines",
            new UpdateJournalEntryLinesRequest(new[]
            {
                new JournalEntryLineRequest("411100", "Correction sauvage", 1.00m, 0m),
                new JournalEntryLineRequest("706100", "Correction sauvage", 0m, 1.00m)
            }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);

        // ... and cannot be cancelled either.
        var cancelPosted = await writerClient.PostAsJsonAsync(
            $"/api/v1/accounting/entries/{draft.Id}/cancel",
            new CancelJournalEntryRequest("Erreur de saisie"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, cancelPosted.StatusCode);

        // The one legal correction: a reversing entry.
        var reverseResponse = await posterClient.PostAsJsonAsync(
            $"/api/v1/accounting/entries/{draft.Id}/reverse",
            new ReverseJournalEntryRequest(null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, reverseResponse.StatusCode);

        var reversal = await reverseResponse.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(reversal);
        Assert.NotEqual(draft.Id, reversal!.Id);
        Assert.Equal(draft.Id, reversal.ReversesEntryId);
        Assert.Equal(EntryStatus.Posted, reversal.Status);
        Assert.Equal("VEA", reversal.JournalCode);
        Assert.Equal(new DateOnly(2026, 4, 15), reversal.EntryDate);

        var reversedClientLine = Assert.Single(reversal.Lines, line => line.AccountCode == "411100");
        Assert.Equal(0m, reversedClientLine.Debit);
        Assert.Equal(11_900.00m, reversedClientLine.Credit);

        var reversedSalesLine = Assert.Single(reversal.Lines, line => line.AccountCode == "706100");
        Assert.Equal(11_900.00m, reversedSalesLine.Debit);
        Assert.Equal(0m, reversedSalesLine.Credit);

        // The corrected entry stays posted and stays in the books, flagged with its reversal.
        var reloadResponse = await posterClient.GetAsync($"/api/v1/accounting/entries/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, reloadResponse.StatusCode);

        var reloaded = await reloadResponse.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal(EntryStatus.Posted, reloaded!.Status);
        Assert.Equal(reversal.Id, reloaded.ReversedByEntryId);

        // An entry is reversed at most once.
        var secondReversal = await posterClient.PostAsJsonAsync(
            $"/api/v1/accounting/entries/{draft.Id}/reverse",
            new ReverseJournalEntryRequest(null, null),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, secondReversal.StatusCode);

        // Both entries are posted and the reversal is exact, so the period nets to zero.
        var balance = await GetTrialBalanceAsync(posterClient, "2026-04-01", "2026-04-30");

        Assert.True(balance.PostedEntriesOnly);
        Assert.Equal(2, balance.AccountCount);
        Assert.Equal(23_800.00m, balance.TotalDebit);
        Assert.Equal(23_800.00m, balance.TotalCredit);
        Assert.Equal(0m, balance.Balance);
        Assert.All(balance.Rows, row => Assert.Equal(0m, row.Balance));
    }

    [Fact]
    public async Task The_trial_balance_counts_posted_entries_only()
    {
        await CreateAccountingUserAsync(
            "accounting.balance",
            "accounting.balance@example.com",
            "Accounting Balance",
            AccountingRead, AccountingWrite, AccountingPost);

        using var client = await _factory.CreateAuthenticatedClientAsync("accounting.balance", Password);

        await CreateAccountAsync(client, "512200", "Banque", AccountKind.Asset);
        await CreateAccountAsync(client, "707200", "Ventes de marchandises", AccountKind.Revenue);
        await CreateJournalAsync(client, "BQB", "Banque");

        var postedId = await CreateEntryAsync(
            client,
            new DateOnly(2026, 6, 10),
            "BQB",
            "Encaissement client",
            new JournalEntryLineRequest("512200", "Banque", 5_000.00m, 0m),
            new JournalEntryLineRequest("707200", "Vente", 0m, 5_000.00m));

        var postResponse = await client.PostAsync($"/api/v1/accounting/entries/{postedId}/post", content: null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        // A perfectly balanced DRAFT, deliberately left unposted: it must not appear anywhere in
        // the balance. This is the rule that surprises people, which is why the response carries
        // the PostedEntriesOnly flag.
        await CreateEntryAsync(
            client,
            new DateOnly(2026, 6, 11),
            "BQB",
            "Brouillon jamais comptabilise",
            new JournalEntryLineRequest("512200", "Banque", 9_999.00m, 0m),
            new JournalEntryLineRequest("707200", "Vente", 0m, 9_999.00m));

        var balance = await GetTrialBalanceAsync(client, "2026-06-01", "2026-06-30");

        Assert.True(balance.PostedEntriesOnly);
        Assert.Equal(2, balance.AccountCount);
        Assert.Equal(5_000.00m, balance.TotalDebit);
        Assert.Equal(5_000.00m, balance.TotalCredit);
        Assert.Equal(0m, balance.Balance);

        var bank = Assert.Single(balance.Rows, row => row.AccountCode == "512200");
        Assert.Equal(5_000.00m, bank.TotalDebit);
        Assert.Equal(0m, bank.TotalCredit);
        Assert.Equal(5_000.00m, bank.Balance);
        Assert.Equal("Banque", bank.AccountLabel);
        Assert.Equal(5, bank.AccountClass);

        var sales = Assert.Single(balance.Rows, row => row.AccountCode == "707200");
        Assert.Equal(0m, sales.TotalDebit);
        Assert.Equal(5_000.00m, sales.TotalCredit);
        Assert.Equal(-5_000.00m, sales.Balance);
    }

    [Fact]
    public async Task Entries_can_only_reference_existing_and_active_accounts()
    {
        await CreateAccountingUserAsync(
            "accounting.refs",
            "accounting.refs@example.com",
            "Accounting References",
            AccountingRead, AccountingWrite);

        await CreateAccountingUserAsync(
            "accounting.reader",
            "accounting.reader@example.com",
            "Accounting Reader",
            AccountingRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("accounting.refs", Password);
        using var readerClient = await _factory.CreateAuthenticatedClientAsync("accounting.reader", Password);

        await CreateAccountAsync(client, "606300", "Entretien et reparations", AccountKind.Expense);
        await CreateJournalAsync(client, "ODC", "Operations diverses");

        // A read-only user can consult the chart but never touch it.
        var readAccounts = await readerClient.GetAsync("/api/v1/accounting/accounts");
        Assert.Equal(HttpStatusCode.OK, readAccounts.StatusCode);

        var forbiddenWrite = await readerClient.PostAsJsonAsync(
            "/api/v1/accounting/accounts",
            new CreateChartAccountRequest("606400", "Assurances", AccountKind.Expense),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);

        // An unknown account is a 404, not a silently created one.
        var unknownAccount = await client.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(
                new DateOnly(2026, 7, 5),
                "ODC",
                "Ecriture sur compte inconnu",
                null,
                new[] { new JournalEntryLineRequest("699999", "Inconnu", 100.00m, 0m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, unknownAccount.StatusCode);

        // A deactivated account keeps its history but accepts no new movement.
        var deactivate = await client.PostAsync("/api/v1/accounting/accounts/606300/deactivate", content: null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var inactiveAccount = await client.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(
                new DateOnly(2026, 7, 5),
                "ODC",
                "Ecriture sur compte desactive",
                null,
                new[] { new JournalEntryLineRequest("606300", "Entretien", 100.00m, 0m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, inactiveAccount.StatusCode);
        Assert.Contains("Inactive", await inactiveAccount.Content.ReadAsStringAsync());

        // A line carrying both a debit and a credit is refused by the domain, surfaced as a 400.
        await client.PostAsync("/api/v1/accounting/accounts/606300/activate", content: null);

        var twoSidedLine = await client.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(
                new DateOnly(2026, 7, 5),
                "ODC",
                "Ligne a deux sens",
                null,
                new[] { new JournalEntryLineRequest("606300", "Entretien", 100.00m, 40.00m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, twoSidedLine.StatusCode);
        Assert.Contains("never both", await twoSidedLine.Content.ReadAsStringAsync());
    }

    private static async Task<ChartAccountResponse> CreateAccountAsync(
        HttpClient client,
        string code,
        string label,
        AccountKind kind)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/accounts",
            new CreateChartAccountRequest(code, label, kind),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var account = await response.Content.ReadFromJsonAsync<ChartAccountResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(account);

        return account!;
    }

    private static async Task CreateJournalAsync(HttpClient client, string code, string label)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/journals",
            new CreateAccountingJournalRequest(code, label),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateEntryAsync(
        HttpClient client,
        DateOnly entryDate,
        string journalCode,
        string label,
        params JournalEntryLineRequest[] lines)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/accounting/entries",
            new CreateJournalEntryRequest(entryDate, journalCode, label, null, lines),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var entry = await response.Content.ReadFromJsonAsync<JournalEntryResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(entry);

        return entry!.Id;
    }

    private static async Task<TrialBalanceResponse> GetTrialBalanceAsync(HttpClient client, string from, string to)
    {
        var response = await client.GetAsync($"/api/v1/accounting/trial-balance?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var balance = await response.Content.ReadFromJsonAsync<TrialBalanceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(balance);

        return balance!;
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given
    /// accounting permission keys. The permissions themselves must already exist (SecuritySeeder
    /// seeds every PermissionCatalog entry during factory initialization) - the assertion below
    /// fails fast with a clear signal if the accounting keys have not been added to
    /// PermissionCatalog yet.
    /// </summary>
    private async Task CreateAccountingUserAsync(
        string userName,
        string email,
        string displayName,
        params string[] permissionKeys)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Accounting permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.accounting.{Guid.NewGuid():N}",
            "Accounting test role",
            "Role dedicated to accounting endpoint tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, displayName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}
