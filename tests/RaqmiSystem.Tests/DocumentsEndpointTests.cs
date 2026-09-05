using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaqmiSystem.Application.Billing;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Application.Identity;
using RaqmiSystem.Application.Security;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Documents;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Infrastructure.Documents;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// Couverture HTTP complete de la chaine documentaire : /api/v1/documents/invoices/{id} et sa route
/// /metadata, contre le vrai Program (routage, JwtBearer, politiques par permission). Les factures
/// sont creees et emises par l'API de facturation, jamais par la table : le document est produit
/// depuis ce que le module Facturation sert.
/// </summary>
public sealed class DocumentsEndpointTests : IClassFixture<RaqmiApiFactory>
{
    private const string Password = "Correct-Horse-Battery-42!";

    private const string CustomersRead = "customers.read";
    private const string CustomersWrite = "customers.write";
    private const string InvoicesRead = "invoices.read";
    private const string InvoicesWrite = "invoices.write";
    private const string InvoicesIssue = "invoices.issue";

    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

    private readonly RaqmiApiFactory _factory;

    public DocumentsEndpointTests(RaqmiApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Issued_invoice_document_is_rendered_once_then_served_byte_for_byte()
    {
        await _factory.ConfigureApplicationSettingsAsync();
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("DOCHTL", "Documents Hotel");

        await CreateDocumentsUserAsync(
            "documents.reader",
            "documents.reader@example.com",
            "Documents Reader",
            CustomersWrite, InvoicesRead, InvoicesWrite, InvoicesIssue);

        using var client = await _factory.CreateAuthenticatedClientAsync("documents.reader", Password);

        await CreateCompanyCustomerAsync(client, "DOCCLI1", "Sonatrach Spa");
        var issued = await CreateAndIssueInvoiceAsync(client, "DOCCLI1", hotelUnitCode);
        Assert.NotNull(issued.Number);

        // Premier appel : rendu + archivage.
        using var firstResponse = await client.GetAsync($"/api/v1/documents/invoices/{issued.Id}");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("application/pdf", firstResponse.Content.Headers.ContentType?.MediaType);

        var firstBytes = await firstResponse.Content.ReadAsByteArrayAsync();
        Assert.True(firstBytes.AsSpan().StartsWith(PdfSignature), "La reponse n'est pas un PDF.");
        Assert.True(firstBytes.Length > 1_000);

        var disposition = firstResponse.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition!.DispositionType);
        Assert.Contains(issued.Number!, disposition.FileName ?? disposition.FileNameStar ?? string.Empty, StringComparison.Ordinal);

        var expectedSha = Convert.ToHexStringLower(SHA256.HashData(firstBytes));
        Assert.Equal(expectedSha, Assert.Single(firstResponse.Headers.GetValues(DocumentHeaders.Sha256)));
        Assert.Equal($"\"{expectedSha}\"", firstResponse.Headers.ETag?.Tag);

        // Second appel : l'archive, octet pour octet - meme si le rendu n'est pas deterministe.
        using var secondResponse = await client.GetAsync($"/api/v1/documents/invoices/{issued.Id}");
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var secondBytes = await secondResponse.Content.ReadAsByteArrayAsync();
        Assert.True(firstBytes.AsSpan().SequenceEqual(secondBytes), "Le second telechargement differe du premier.");
        Assert.Equal(expectedSha, Assert.Single(secondResponse.Headers.GetValues(DocumentHeaders.Sha256)));

        // Les metadonnees decrivent la piece servie.
        using var metadataResponse = await client.GetAsync($"/api/v1/documents/invoices/{issued.Id}/metadata");
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);

        var metadata = await metadataResponse.Content.ReadFromJsonAsync<DocumentMetadataResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(metadata);
        Assert.Equal(DocumentType.Invoice, metadata!.Type);
        Assert.Equal(issued.Number, metadata.Reference);
        Assert.Equal(expectedSha, metadata.Sha256);
        Assert.Equal(firstBytes.LongLength, metadata.SizeBytes);
        Assert.Equal(QuestPdfInvoiceRenderer.CurrentTemplateVersion, metadata.TemplateVersion);
        Assert.Equal("application/pdf", metadata.ContentType);
        Assert.Equal($"{issued.Number}.pdf", metadata.FileName);
        Assert.Equal("documents.reader", metadata.RenderedBy);

        // Une seule ligne archivee, et un seul rendu audite.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();

        var archived = await dbContext.Set<RenderedDocument>()
            .Where(document => document.Type == DocumentType.Invoice && document.Reference == issued.Number)
            .ToArrayAsync();

        var single = Assert.Single(archived);
        Assert.Equal(expectedSha, single.Sha256);
        Assert.Equal(metadata.Id, single.Id);
        Assert.True(single.Content.AsSpan().SequenceEqual(firstBytes));

        var auditEntries = await dbContext.AuditLogs
            .Where(log => log.Action == DocumentService.InvoiceRenderedAuditAction && log.EntityId == single.Id.ToString())
            .CountAsync();

        Assert.Equal(1, auditEntries);
    }

    [Fact]
    public async Task Draft_invoice_has_no_legal_document()
    {
        await _factory.ConfigureApplicationSettingsAsync();
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("DOCDRF", "Documents Draft Hotel");

        await CreateDocumentsUserAsync(
            "documents.drafter",
            "documents.drafter@example.com",
            "Documents Drafter",
            CustomersWrite, InvoicesRead, InvoicesWrite);

        using var client = await _factory.CreateAuthenticatedClientAsync("documents.drafter", Password);

        await CreateCompanyCustomerAsync(client, "DOCCLI2", "Client Brouillon Sarl");
        var draft = await CreateInvoiceAsync(client, "DOCCLI2", hotelUnitCode);
        Assert.Equal(InvoiceStatus.Draft, draft.Status);

        using var pdfResponse = await client.GetAsync($"/api/v1/documents/invoices/{draft.Id}");
        Assert.Equal(HttpStatusCode.NotFound, pdfResponse.StatusCode);

        using var metadataResponse = await client.GetAsync($"/api/v1/documents/invoices/{draft.Id}/metadata");
        Assert.Equal(HttpStatusCode.NotFound, metadataResponse.StatusCode);

        // Rien n'a ete archive pour un brouillon : il n'a pas de reference.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RaqmiDbContext>();
        Assert.False(await dbContext.Set<RenderedDocument>().AnyAsync(document => document.RenderedBy == "documents.drafter"));
    }

    [Fact]
    public async Task Reading_a_document_requires_invoices_read()
    {
        // Un profil qui lit les clients mais pas les factures ne lit pas leurs documents non plus.
        await CreateDocumentsUserAsync(
            "documents.outsider",
            "documents.outsider@example.com",
            "Documents Outsider",
            CustomersRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("documents.outsider", Password);

        using var pdfResponse = await client.GetAsync($"/api/v1/documents/invoices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, pdfResponse.StatusCode);

        using var metadataResponse = await client.GetAsync($"/api/v1/documents/invoices/{Guid.NewGuid()}/metadata");
        Assert.Equal(HttpStatusCode.Forbidden, metadataResponse.StatusCode);

        using var anonymous = _factory.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync($"/api/v1/documents/invoices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    [Fact]
    public async Task Unknown_invoice_is_not_found()
    {
        await CreateDocumentsUserAsync(
            "documents.seeker",
            "documents.seeker@example.com",
            "Documents Seeker",
            InvoicesRead);

        using var client = await _factory.CreateAuthenticatedClientAsync("documents.seeker", Password);

        using var response = await client.GetAsync($"/api/v1/documents/invoices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rendered_document_carries_the_invoice_figures_and_nothing_else()
    {
        await _factory.ConfigureApplicationSettingsAsync();
        var hotelUnitCode = await _factory.CreateHotelUnitAsync("DOCTOT", "Documents Totals Hotel");

        await CreateDocumentsUserAsync(
            "documents.auditor",
            "documents.auditor@example.com",
            "Documents Auditor",
            CustomersWrite, InvoicesRead, InvoicesWrite, InvoicesIssue);

        using var baseClient = await _factory.CreateAuthenticatedClientAsync("documents.auditor", Password);

        await CreateCompanyCustomerAsync(baseClient, "DOCCLI3", "Naftal Spa");
        var issued = await CreateAndIssueInvoiceAsync(baseClient, "DOCCLI3", hotelUnitCode);

        // Le vrai gabarit, enveloppe d'un capteur : on observe le modele exact que le PDF imprime,
        // dans le vrai pipeline HTTP (meme base SQLite, hote derive avec le rendu remplace).
        var capturing = new CapturingInvoiceRenderer();

        using var derivedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDocumentRenderer<InvoiceDocumentModel>>();
                services.AddSingleton<IDocumentRenderer<InvoiceDocumentModel>>(capturing);
            });
        });

        using var derivedClient = derivedFactory.CreateClient();
        await AuthenticateAsync(derivedClient, "documents.auditor");

        using var response = await derivedClient.GetAsync($"/api/v1/documents/invoices/{issued.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renderedBytes = await response.Content.ReadAsByteArrayAsync();

        var model = Assert.Single(capturing.Models);
        Assert.Equal(issued.Id, model.InvoiceId);
        Assert.Equal(issued.Number, model.Number);

        // Totaux : ceux de la facture, tels que le module Facturation les sert.
        Assert.Equal(issued.TotalExclVat, model.TotalExclVat);
        Assert.Equal(issued.TotalVat, model.TotalVat);
        Assert.Equal(issued.TotalInclVat, model.TotalInclVat);

        // Lignes : recopiees une a une.
        Assert.Equal(issued.Lines.Count, model.Lines.Count);

        foreach (var line in model.Lines)
        {
            var source = issued.Lines.Single(candidate => candidate.LineNumber == line.LineNumber);
            Assert.Equal(source.Designation, line.Designation);
            Assert.Equal(source.Quantity, line.Quantity);
            Assert.Equal(source.UnitPrice, line.UnitPrice);
            Assert.Equal(source.VatRate, line.VatRate);
            Assert.Equal(source.LineTotalExclVat, line.LineTotalExclVat);
        }

        // Recapitulatif : trois taux, dont la somme retombe sur les totaux de la facture.
        Assert.Equal([0m, 9m, 19m], model.VatSummary.Select(row => row.VatRate).ToArray());
        Assert.Equal(issued.TotalExclVat, model.VatSummary.Sum(row => row.BaseExclVat));
        Assert.Equal(issued.TotalVat, model.VatSummary.Sum(row => row.VatAmount));

        // Emetteur : l'instantane fige a l'emission.
        Assert.Equal(issued.IssuerName, model.Issuer.Name);
        Assert.Equal(issued.IssuerNif, model.Issuer.Nif);
        Assert.Equal("Naftal Spa", model.Customer.Name);

        // L'hote d'origine sert la meme archive sans re-rendre : un seul modele capture.
        using var replay = await baseClient.GetAsync($"/api/v1/documents/invoices/{issued.Id}");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayedBytes = await replay.Content.ReadAsByteArrayAsync();
        Assert.True(renderedBytes.AsSpan().SequenceEqual(replayedBytes));
        Assert.Single(capturing.Models);
    }

    private static async Task CreateCompanyCustomerAsync(HttpClient client, string code, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/billing/customers",
            new CreateCustomerRequest(
                Code: code,
                Name: name,
                CustomerType: CustomerType.Company,
                Nif: "099912345678901",
                Rc: "16/00-9876543A11",
                Ai: "16098765432",
                Nis: "123456789012345",
                Address: "Djenane El Malik, Hydra",
                City: "Alger"),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>Trois lignes, trois taux (0, 9 et 19 %) : le recapitulatif TVA a de quoi exister.</summary>
    private static async Task<InvoiceResponse> CreateInvoiceAsync(HttpClient client, string customerCode, string hotelUnitCode)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/billing/invoices",
            new CreateInvoiceRequest(
                CustomerCode: customerCode,
                HotelUnitCode: hotelUnitCode,
                InvoiceDate: new DateOnly(2026, 3, 10),
                Lines: new[]
                {
                    new InvoiceLineRequest("Taxe de séjour", 2m, 200.00m, 0m),
                    new InvoiceLineRequest("Hébergement chambre double", 2m, 12_500.00m, 9m),
                    new InvoiceLineRequest("Restauration", 3m, 1_850.50m, 19m)
                }),
            RaqmiApiFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(invoice);
        return invoice!;
    }

    private static async Task<InvoiceResponse> CreateAndIssueInvoiceAsync(HttpClient client, string customerCode, string hotelUnitCode)
    {
        var draft = await CreateInvoiceAsync(client, customerCode, hotelUnitCode);

        var response = await client.PostAsync($"/api/v1/billing/invoices/{draft.Id}/issue", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var issued = await response.Content.ReadFromJsonAsync<InvoiceResponse>(RaqmiApiFactory.JsonOptions);
        Assert.NotNull(issued);
        Assert.Equal(InvoiceStatus.Issued, issued!.Status);
        return issued;
    }

    /// <summary>
    /// Connexion sur un client d'hote derive : le jeton doit etre emis par l'hote qui le validera
    /// (chaque hote de test tire sa propre cle de signature ephemere).
    /// </summary>
    private static async Task AuthenticateAsync(HttpClient client, string userName)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(userName, Password),
            RaqmiApiFactory.JsonOptions);

        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(RaqmiApiFactory.JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private async Task CreateDocumentsUserAsync(
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
            "Permission keys are missing from the seeded PermissionCatalog: " +
            string.Join(", ", permissionKeys.Except(permissions.Select(permission => permission.Key))));

        var role = new Role(
            $"test.documents.{Guid.NewGuid():N}",
            "Documents test role",
            "Role dedicated to documents endpoint tests.");

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

    /// <summary>Le vrai gabarit, plus la memoire du dernier modele qu'il a recu.</summary>
    private sealed class CapturingInvoiceRenderer : IDocumentRenderer<InvoiceDocumentModel>
    {
        private readonly QuestPdfInvoiceRenderer _inner = new();

        public List<InvoiceDocumentModel> Models { get; } = [];

        public int TemplateVersion => _inner.TemplateVersion;

        public byte[] Render(InvoiceDocumentModel model)
        {
            Models.Add(model);
            return _inner.Render(model);
        }
    }
}
