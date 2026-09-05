using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaqmiSystem.Domain.Documents;
using RaqmiSystem.Infrastructure.Documents;
using RaqmiSystem.Infrastructure.Persistence;

namespace RaqmiSystem.Tests;

/// <summary>
/// L'archive et son entite, sans HTTP : l'index unique (type, reference) tient la regle
/// « premier ecrit gagne », et RenderedDocument refuse tout ce qui n'est pas une piece archivable.
/// SQLite en memoire construit le schema depuis le modele (EnsureCreated), comme RaqmiApiFactory.
/// </summary>
public sealed class DocumentsArchiveTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var dbContext = CreateContext();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Storing_the_same_reference_twice_keeps_the_first_document_byte_for_byte()
    {
        var first = RenderedDocument.Create(
            DocumentType.Invoice,
            "FAC-2026-000007",
            templateVersion: 1,
            FakePdf("premier rendu"),
            "caissier.1",
            new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.Zero));

        var second = RenderedDocument.Create(
            DocumentType.Invoice,
            "fac-2026-000007 ",
            templateVersion: 2,
            FakePdf("second rendu, gabarit plus recent"),
            "caissier.2",
            new DateTimeOffset(2026, 4, 2, 8, 0, 0, TimeSpan.Zero));

        // Deux contextes distincts : deux requetes concurrentes, chacune avec son unite de travail.
        await using (var dbContext = CreateContext())
        {
            var stored = await new EfDocumentArchive(dbContext).StoreAsync(first, CancellationToken.None);
            Assert.Same(first, stored);
        }

        RenderedDocument served;

        await using (var dbContext = CreateContext())
        {
            served = await new EfDocumentArchive(dbContext).StoreAsync(second, CancellationToken.None);
        }

        // Le second rendu est abandonne : c'est le premier qui est servi, avec son gabarit, son
        // auteur et ses octets.
        Assert.NotSame(second, served);
        Assert.Equal(first.Id, served.Id);
        Assert.Equal(first.Sha256, served.Sha256);
        Assert.Equal(1, served.TemplateVersion);
        Assert.Equal("caissier.1", served.RenderedBy);
        Assert.True(first.Content.AsSpan().SequenceEqual(served.Content));

        await using (var dbContext = CreateContext())
        {
            Assert.Equal(1, await dbContext.Set<RenderedDocument>().CountAsync());

            var found = await new EfDocumentArchive(dbContext).FindAsync(DocumentType.Invoice, "  fac-2026-000007", CancellationToken.None);
            Assert.NotNull(found);
            Assert.Equal(first.Id, found!.Id);
        }
    }

    [Fact]
    public async Task Find_returns_null_for_a_reference_that_was_never_rendered()
    {
        await using var dbContext = CreateContext();

        var found = await new EfDocumentArchive(dbContext).FindAsync(DocumentType.Invoice, "FAC-2026-999999", CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public void Create_computes_the_sha256_and_size_and_normalises_the_reference()
    {
        var content = FakePdf("contenu");

        var document = RenderedDocument.Create(DocumentType.Invoice, " fac-2026-000001 ", 1, content, "  ", DateTimeOffset.UtcNow);

        Assert.Equal("FAC-2026-000001", document.Reference);
        Assert.Equal("FAC-2026-000001.pdf", document.FileName);
        Assert.Equal(content.LongLength, document.SizeBytes);
        Assert.Equal(RenderedDocument.Sha256HexLength, document.Sha256.Length);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)), document.Sha256);
        Assert.Equal("system", document.RenderedBy);
        Assert.Equal(RenderedDocument.PdfContentType, document.ContentType);

        // Copie defensive : modifier le tampon d'origine ne touche pas l'archive.
        content[^1] = (byte)'X';
        Assert.NotEqual(content[^1], document.Content[^1]);
    }

    [Fact]
    public void Create_refuses_empty_content_non_pdf_content_and_a_missing_reference()
    {
        Assert.Throws<ArgumentException>(() =>
            RenderedDocument.Create(DocumentType.Invoice, "FAC-2026-000001", 1, ReadOnlySpan<byte>.Empty, "x", DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            RenderedDocument.Create(DocumentType.Invoice, "FAC-2026-000001", 1, "<html>pas un pdf</html>"u8, "x", DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            RenderedDocument.Create(DocumentType.Invoice, "   ", 1, FakePdf("x"), "x", DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RenderedDocument.Create(DocumentType.Invoice, "FAC-2026-000001", 0, FakePdf("x"), "x", DateTimeOffset.UtcNow));
    }

    private RaqmiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RaqmiDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new RaqmiDbContext(options);
    }

    private static byte[] FakePdf(string marker)
    {
        // L'archive ne valide que la signature d'en-tete : un faux PDF suffit a ces tests, le vrai
        // gabarit est couvert par DocumentsInvoiceRendererTests.
        return Encoding.ASCII.GetBytes($"%PDF-1.7\n% {marker}\n%%EOF\n");
    }
}
