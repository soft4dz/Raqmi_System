using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class FolioChargeConfiguration : IEntityTypeConfiguration<FolioCharge>
{
    public void Configure(EntityTypeBuilder<FolioCharge> builder)
    {
        builder.ToTable("folio_charges", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_folio_charges_kind",
                "kind IN ('Night', 'Extra', 'Settlement', 'Adjustment')");

            // The sign rule of the domain, restated for any writer that bypasses the entity:
            // no zero lines, and a negative amount only on Settlement/Adjustment. CAST because
            // the SQLite test provider stores decimal as TEXT.
            table.HasCheckConstraint(
                "ck_folio_charges_amount_nonzero",
                "CAST(amount AS numeric) <> 0");

            table.HasCheckConstraint(
                "ck_folio_charges_amount_sign",
                "kind IN ('Settlement', 'Adjustment') OR CAST(amount AS numeric) > 0");
        });

        builder.HasKey(charge => charge.Id);

        // ValueGeneratedNever is load-bearing, not decoration (same rationale as BudgetLine and
        // JournalEntryLine): FolioCharge assigns its own Id, and a charge added to an ALREADY
        // TRACKED folio (AddFolioChargeAsync loads the folio before folio.AddCharge) is
        // discovered through the navigation with its key already set. With a value-generated
        // key, change tracking would classify that discovered line as an existing row (Modified,
        // an UPDATE affecting 0 rows -> DbUpdateConcurrencyException) instead of a new one.
        builder.Property(charge => charge.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(charge => charge.FolioId)
            .HasColumnName("folio_id");

        builder.Property(charge => charge.LineNumber)
            .HasColumnName("line_number");

        builder.Property(charge => charge.ChargeDate)
            .HasColumnName("charge_date");

        builder.Property(charge => charge.Label)
            .HasColumnName("label")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(charge => charge.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2);

        builder.Property(charge => charge.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(charge => charge.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100);

        builder.HasIndex(charge => charge.FolioId)
            .HasDatabaseName("ix_folio_charges_folio_id");
    }
}
