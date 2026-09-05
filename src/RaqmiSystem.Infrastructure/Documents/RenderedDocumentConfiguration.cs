using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Documents;

namespace RaqmiSystem.Infrastructure.Documents;

/// <summary>
/// Table documents.rendered_documents. L'index unique (type, reference) EST la regle
/// d'immuabilite : deux rendus concurrents de la meme facture ne peuvent pas cohabiter, et c'est
/// la base qui tranche, pas l'ordre d'arrivee dans le code (voir EfDocumentArchive.StoreAsync).
/// Ramassee par ApplyConfigurationsFromAssembly : RaqmiDbContext n'a pas besoin de DbSet dedie.
/// </summary>
public sealed class RenderedDocumentConfiguration : IEntityTypeConfiguration<RenderedDocument>
{
    public const string UniqueTypeReferenceIndexName = "ux_rendered_documents_type_reference";

    public void Configure(EntityTypeBuilder<RenderedDocument> builder)
    {
        builder.ToTable("rendered_documents", "documents", table =>
        {
            table.HasCheckConstraint(
                "ck_rendered_documents_type",
                "type IN ('Invoice')");

            table.HasCheckConstraint(
                "ck_rendered_documents_size_bytes",
                "size_bytes > 0");

            // length() existe sous PostgreSQL comme sous SQLite : une empreinte tronquee ne rentre pas.
            table.HasCheckConstraint(
                "ck_rendered_documents_sha256",
                $"length(sha256) = {RenderedDocument.Sha256HexLength}");
        });

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id).HasColumnName("id");

        builder.Property(document => document.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(document => document.Reference)
            .HasColumnName("reference")
            .HasMaxLength(RenderedDocument.ReferenceMaxLength)
            .IsRequired();

        builder.Property(document => document.TemplateVersion)
            .HasColumnName("template_version");

        builder.Property(document => document.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(RenderedDocument.Sha256HexLength)
            .IsRequired();

        builder.Property(document => document.SizeBytes)
            .HasColumnName("size_bytes");

        builder.Property(document => document.RenderedAt)
            .HasColumnName("rendered_at");

        builder.Property(document => document.RenderedBy)
            .HasColumnName("rendered_by")
            .HasMaxLength(160)
            .IsRequired();

        // byte[] -> bytea sous Npgsql, BLOB sous SQLite : aucun type de colonne force, pour que le
        // meme modele serve la production et les tests.
        builder.Property(document => document.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Ignore(document => document.ContentType);
        builder.Ignore(document => document.FileName);

        builder.HasIndex(document => new { document.Type, document.Reference })
            .IsUnique()
            .HasDatabaseName(UniqueTypeReferenceIndexName);

        builder.HasIndex(document => document.RenderedAt)
            .HasDatabaseName("ix_rendered_documents_rendered_at");
    }
}
