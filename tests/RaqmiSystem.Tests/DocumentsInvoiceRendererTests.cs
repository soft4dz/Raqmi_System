using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using RaqmiSystem.Application.Documents;
using RaqmiSystem.Domain.Documents;
using RaqmiSystem.Infrastructure.Documents;

namespace RaqmiSystem.Tests;

/// <summary>
/// Le gabarit QuestPDF, seul : produit-il un PDF valide, sur combien de pages, sous quelle version.
/// Le nombre de pages est compte par QuestPDF lui-meme (une image par page) plutot qu'en fouillant
/// le PDF produit, dont les objets peuvent etre compresses.
/// </summary>
public sealed class DocumentsInvoiceRendererTests
{
    private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

    [Fact]
    public void Renders_a_valid_single_page_pdf_for_an_invoice_with_three_vat_rates()
    {
        var model = InvoiceDocumentModelBuilder.Build(
            DocumentsTestData.IssuedInvoice(),
            DocumentsTestData.CompanyCustomer(),
            DocumentsTestData.Settings());

        var renderer = new QuestPdfInvoiceRenderer();

        var pdf = renderer.Render(model);

        Assert.True(pdf.Length > 1_000, $"Le PDF fait {pdf.Length} octets : trop peu pour une facture.");
        Assert.True(pdf.AsSpan().StartsWith(PdfSignature), "Le contenu ne commence pas par la signature %PDF-.");
        Assert.Equal(1, CountPages(model));

        // Ce que l'archive calculera : l'empreinte est celle des octets rendus, ni plus ni moins.
        Assert.Equal(RenderedDocument.ComputeSha256(pdf), RenderedDocument.ComputeSha256(pdf.AsSpan()));
    }

    [Fact]
    public void A_long_invoice_paginates_instead_of_failing()
    {
        var lines = Enumerable.Range(1, 90)
            .Select(number => DocumentsTestData.Line(
                number,
                $"Prestation n° {number} - hébergement, restauration ou service annexe",
                1m + number % 3,
                1_000m + number,
                (number % 3) switch { 0 => 0m, 1 => 9m, _ => 19m }))
            .ToArray();

        var model = InvoiceDocumentModelBuilder.Build(
            DocumentsTestData.IssuedInvoice(lines: lines),
            DocumentsTestData.CompanyCustomer(),
            DocumentsTestData.Settings());

        var pdf = new QuestPdfInvoiceRenderer().Render(model);

        Assert.True(pdf.AsSpan().StartsWith(PdfSignature));
        Assert.True(CountPages(model) >= 2, "90 lignes doivent deborder sur au moins deux pages.");
    }

    [Fact]
    public void Renders_an_individual_customer_and_a_bare_issuer_without_optional_mentions()
    {
        // Emetteur reduit au minimum legal (nom + NIF), client particulier : aucune ligne
        // optionnelle ne doit faire echouer le gabarit.
        var invoice = DocumentsTestData.IssuedInvoice(customerName: "Karim Benali") with
        {
            IssuerRc = null,
            IssuerAi = null,
            IssuerNis = null,
            IssuerAddress = null
        };

        var settings = DocumentsTestData.Settings() with { CompanyCity = null, CompanyPhone = null, CompanyEmail = null };

        var model = InvoiceDocumentModelBuilder.Build(invoice, DocumentsTestData.IndividualCustomer(), settings);

        var pdf = new QuestPdfInvoiceRenderer().Render(model);

        Assert.True(pdf.AsSpan().StartsWith(PdfSignature));
        Assert.Equal(1, CountPages(model));
    }

    [Fact]
    public void Template_version_is_the_one_archived_with_each_document()
    {
        Assert.Equal(QuestPdfInvoiceRenderer.CurrentTemplateVersion, new QuestPdfInvoiceRenderer().TemplateVersion);
        Assert.True(QuestPdfInvoiceRenderer.CurrentTemplateVersion >= 1);
    }

    private static int CountPages(InvoiceDocumentModel model)
    {
        QuestPdfInvoiceRenderer.ConfigureEngine();

        return QuestPdfInvoiceRenderer.Compose(model)
            .GenerateImages(new ImageGenerationSettings { RasterDpi = 36 })
            .Count();
    }
}
