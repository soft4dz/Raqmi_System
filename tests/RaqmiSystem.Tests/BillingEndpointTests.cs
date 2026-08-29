using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Full-HTTP integration coverage for the billing module (customers and sales invoices).
/// Each test provisions its own dedicated role carrying exactly the billing permission keys it
/// needs (the keys are seeded from PermissionCatalog by SecuritySeeder during factory startup),
/// so the per-permission authorization policies registered in Program.cs are enforced for real.
/// Invoice numbers follow the ISSUE year (UtcNow at issue time), not the backdatable invoice
/// date, so both tests share the current year's sequence: the assertions are written to be
/// independent of the order xunit runs them in (relative sequence checks, no absolute numbers).
/// </summary>
public sealed class BillingEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string CustomersRead = "customers.read";
    private const string CustomersWrite = "customers.write";
    private const string InvoicesRead = "invoices.read";
    private const string InvoicesWrite = "invoices.write";
    private const string InvoicesIssue = "invoices.issue";

    private readonly RaqmiApiFactory _factory;

    public BillingEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Invoice_goes_through_the_full_draft_issue_paid_cycle_with_the_right_permissions()
    {
        // Issuance is refused while the establishment is not identified (see
        // InvoiceIssuanceIssuerIdentityTests): this module's tests take that identity as given.
        await _factory.ConfigureApplicationSettingsAsync();

        var hotelUnitCode = await _factory.CreateHotelUnitAsync("BILHTL", "Billing Hotel");

        await CreateBillingUserAsync(
            "billing.writer",
            "billing.writer@example.com",
            "Billing Writer",
            CustomersRead, CustomersWrite, InvoicesRead, InvoicesWrite);

        await CreateBillingUserAsync(
            "billing.issuer",
            "billing.issuer@example.com",
            "Billing Issuer",
            InvoicesRead, InvoicesIssue);

        using var writerClient = await _factory.CreateAuthenticatedClientAsync("billing.writer", Password);

        var customerResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest(
                Code: "sonatrach",
                Name: "Sonatrach Spa",
                CustomerType: CustomerType.Company,
                Nif: "098765432112345",
                City: "Alger"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);

        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(customer);
        Assert.Equal("SONATRACH", customer!.Code);

        var createResponse = await writerClient.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                CustomerCode: "SONATRACH",
                HotelUnitCode: hotelUnitCode,
                InvoiceDate: new DateOnly(2026, 3, 10),
                Lines: new[]
                {
                    new InvoiceLineRequest("Hebergement chambre double", 2m, 12_500.00m, 9m),
                    new InvoiceLineRequest("Restauration", 3m, 1_850.50m, 19m)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);
        Assert.Equal(InvoiceStatus.Draft, draft!.Status);
        Assert.Null(draft.Number);
        Assert.Equal(30_551.50m, draft.TotalExclVat);
        Assert.Equal(3_304.79m, draft.TotalVat);
        Assert.Equal(33_856.29m, draft.TotalInclVat);

        // The writer has invoices.write but not invoices.issue: issuing is a distinct engaging act.
        var forbiddenIssue = await writerClient.PostAsync($"/api/v1/billing/invoices/{draft.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenIssue.StatusCode);

        using var issuerClient = await _factory.CreateAuthenticatedClientAsync("billing.issuer", Password);

        var issueResponse = await issuerClient.PostAsync($"/api/v1/billing/invoices/{draft.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var issued = await issueResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued!.Status);

        // The legal number follows the ISSUE year (UtcNow), not the invoice date.
        Assert.NotNull(issued.Number);
        Assert.Matches($@"^FAC-{DateTime.UtcNow.Year}-\d{{6}}$", issued.Number!);

        // Legal immutability: renaming the customer after issuance must not rewrite the
        // issued invoice, which keeps rendering the identification frozen at issue time.
        var renameResponse = await writerClient.PutAsJsonAsync(
            "/api/v1/billing/customers/SONATRACH",
            new UpdateCustomerRequest(
                Name: "Sonatrach Renommee Spa",
                CustomerType: CustomerType.Company,
                Nif: "098765432112345",
                City: "Alger"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);

        var reloadResponse = await writerClient.GetAsync($"/api/v1/billing/invoices/{draft.Id}");
        Assert.Equal(HttpStatusCode.OK, reloadResponse.StatusCode);

        var reloaded = await reloadResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(reloaded);
        Assert.Equal("Sonatrach Spa", reloaded!.CustomerName);

        var payResponse = await writerClient.PostAsync($"/api/v1/billing/invoices/{draft.Id}/pay", content: null);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        var paid = await payResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(paid);
        Assert.Equal(InvoiceStatus.Paid, paid!.Status);
        Assert.Equal("billing.writer", paid.PaidBy);
    }

    [Fact]
    public async Task Issuing_two_invoices_of_the_same_year_allocates_sequential_numbers()
    {
        await _factory.ConfigureApplicationSettingsAsync();

        var hotelUnitCode = await _factory.CreateHotelUnitAsync("SEQHTL", "Sequence Hotel");

        await CreateBillingUserAsync(
            "billing.sequencer",
            "billing.sequencer@example.com",
            "Billing Sequencer",
            CustomersWrite, InvoicesRead, InvoicesWrite, InvoicesIssue);

        using var client = await _factory.CreateAuthenticatedClientAsync("billing.sequencer", Password);

        var customerResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest("SEQCLI", "Client Sequence", CustomerType.Individual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);

        // Both invoices are antedated to a past year: the numbering must nevertheless follow
        // the year of issuance (now), and the second issue must take the next sequence slot.
        var firstNumber = await CreateAndIssueAsync(client, hotelUnitCode, new DateOnly(2025, 6, 1));
        var secondNumber = await CreateAndIssueAsync(client, hotelUnitCode, new DateOnly(2025, 6, 2));

        var issueYear = DateTime.UtcNow.Year;
        var firstSequence = ParseSequence(firstNumber, issueYear);
        var secondSequence = ParseSequence(secondNumber, issueYear);

        Assert.Equal(firstSequence + 1, secondSequence);
    }

    private static int ParseSequence(string? number, int expectedYear)
    {
        Assert.NotNull(number);
        Assert.Matches($@"^FAC-{expectedYear}-\d{{6}}$", number!);

        return int.Parse(number!.Split('-')[2], CultureInfo.InvariantCulture);
    }

    private static async Task<string?> CreateAndIssueAsync(
        HttpClient client,
        string hotelUnitCode,
        DateOnly invoiceDate)
    {
        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                CustomerCode: "SEQCLI",
                HotelUnitCode: hotelUnitCode,
                InvoiceDate: invoiceDate,
                Lines: new[] { new InvoiceLineRequest("Hebergement", 1m, 8_000.00m, 9m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        var issueResponse = await client.PostAsync($"/api/v1/billing/invoices/{draft!.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, issueResponse.StatusCode);

        var issued = await issueResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(issued);

        return issued!.Number;
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given billing
    /// permission keys. The permissions themselves must already exist (SecuritySeeder seeds every
    /// PermissionCatalog entry during factory initialization) - the assertion below fails fast
    /// with a clear signal if the billing keys have not been added to PermissionCatalog yet.
    /// </summary>
    private async Task CreateBillingUserAsync(
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
            "Billing permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.billing.{Guid.NewGuid():N}",
            "Billing test role",
            "Role dedicated to billing endpoint tests.");

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
