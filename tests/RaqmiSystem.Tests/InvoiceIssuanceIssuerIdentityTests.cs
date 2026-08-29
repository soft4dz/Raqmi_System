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
/// Shared harness for the two installations that must NOT be able to issue an invoice: the one
/// where the global settings were never written, and the one where they were written without the
/// mandatory legal mentions. Each concrete class below owns its own factory (and therefore its own
/// in-memory database), because the very thing under test is the state of the settings singleton -
/// a single shared database could not host both a virgin and a half-configured installation.
/// </summary>
public abstract class InvoiceIssuanceIssuerIdentityTestsBase
{
    protected const string Password = "Correct-Horse-Battery-42!";

    protected InvoiceIssuanceIssuerIdentityTestsBase(RaqmiApiFactory factory)
    {
        Factory = factory;
    }

    protected RaqmiApiFactory Factory { get; }

    /// <summary>
    /// Creates a draft invoice for a brand new customer and returns it, then asserts that issuing
    /// it is refused with a 400 whose message carries <paramref name="expectedMessageFragment"/>,
    /// that the invoice is still an untouched Draft, and - the point of the whole guard - that no
    /// number was allocated out of the legal sequence.
    /// </summary>
    protected async Task AssertIssuanceIsRefusedAsync(string expectedMessageFragment)
    {
        var hotelUnitCode = await Factory.CreateHotelUnitAsync("GUARDH", "Guard Hotel");

        await CreateIssuingUserAsync(
            "billing.guard",
            "billing.guard@example.com",
            PermissionCatalog.CustomersWrite,
            PermissionCatalog.InvoicesRead,
            PermissionCatalog.InvoicesWrite,
            PermissionCatalog.InvoicesIssue);

        using var client = await Factory.CreateAuthenticatedClientAsync("billing.guard", Password);

        var customerResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest("GRDCLI", "Client Garde", CustomerType.Individual),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                CustomerCode: "GRDCLI",
                HotelUnitCode: hotelUnitCode,
                InvoiceDate: new DateOnly(2026, 3, 10),
                Lines: new[] { new InvoiceLineRequest("Hebergement", 1m, 8_000.00m, 9m) }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var draft = await createResponse.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(draft);

        var issueResponse = await client.PostAsync($"/api/v1/billing/invoices/{draft!.Id}/issue", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, issueResponse.StatusCode);
        Assert.Contains(expectedMessageFragment, await issueResponse.Content.ReadAsStringAsync());

        // The refusal must be total: still a draft, no emitter frozen, no legal number.
        var reloaded = await client.GetFromJsonAsync<InvoiceResponse>(
            $"/api/v1/billing/invoices/{draft.Id}",
            RaqmiApiFactory.JsonOptions);

        Assert.NotNull(reloaded);
        Assert.Equal(InvoiceStatus.Draft, reloaded!.Status);
        Assert.Null(reloaded.Number);
        Assert.Null(reloaded.IssuerName);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        // No rank of the legal sequence was consumed: a refused issuance must not create a hole.
        Assert.Empty(await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.IssuedSequence != null)
            .ToArrayAsync());

        Assert.Empty(await dbContext.AuditLogs
            .AsNoTracking()
            .Where(entry => entry.Action == "finance.invoice.issued")
            .ToArrayAsync());
    }

    /// <summary>
    /// Creates a user attached to a fresh single-purpose role carrying exactly the given permission
    /// keys, mirroring BillingEndpointTests so the per-permission authorization policies registered
    /// in Program.cs are enforced for real against that user's token.
    /// </summary>
    private async Task CreateIssuingUserAsync(
        string userName,
        string email,
        params string[] permissionKeys)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionKeys.Contains(permission.Key))
            .ToArrayAsync();

        Assert.True(
            permissions.Length == permissionKeys.Length,
            "Permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.issuance.{Guid.NewGuid():N}",
            "Issuance test role",
            "Role dedicated to the invoice issuance guard tests.");

        foreach (var permission in permissions)
        {
            role.GrantPermission(permission, DateTimeOffset.UtcNow);
        }

        dbContext.Roles.Add(role);

        var user = new User(userName, email, userName, passwordHasher.Hash(Password), mustChangePassword: false);
        user.AssignRole(role, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}

/// <summary>
/// Fresh installation: nobody has ever opened the "Parametrage global" module, so the settings
/// singleton does not exist and a read returns the placeholder defaults. Issuing an invoice there
/// would freeze "Etablissement non configure" with no NIF/RC/AI/address into a legal document
/// AND burn a rank of the numbering sequence, irreversibly.
/// </summary>
public sealed class InvoiceIssuanceWithoutSettingsTests(RaqmiApiFactory factory)
    : InvoiceIssuanceIssuerIdentityTestsBase(factory), IClassFixture<RaqmiApiFactory>
{
    [Fact]
    public async Task Issuing_an_invoice_is_refused_while_the_establishment_is_not_configured()
    {
        await AssertIssuanceIsRefusedAsync("global settings must be filled in");
    }
}

/// <summary>
/// Half-configured installation: the settings row exists (IsConfigured is true) but the update
/// request only ever required a company name, so the establishment can be named without being
/// fiscally identified. The mandatory mentions of an Algerian invoice are checked one by one.
/// </summary>
public sealed class InvoiceIssuanceWithIncompleteSettingsTests(RaqmiApiFactory factory)
    : InvoiceIssuanceIssuerIdentityTestsBase(factory), IClassFixture<RaqmiApiFactory>
{
    [Fact]
    public async Task Issuing_an_invoice_is_refused_while_the_establishment_has_no_nif()
    {
        await Factory.ConfigureApplicationSettingsAsync(companyNif: null);

        await AssertIssuanceIsRefusedAsync("Missing from the global settings: NIF.");
    }
}
