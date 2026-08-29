using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Infrastructure.Billing;

public sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        // No "vat_rate IN (0, 9, 19)" check constraint here on purpose: SQLite (used by the
        // integration-test harness) stores decimals as TEXT, so a numeric IN-list would reject
        // every row there. The allowed-rates invariant is enforced by the InvoiceLine constructor.
        builder.ToTable("invoice_lines", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_invoice_lines_line_number_positive",
                "line_number >= 1");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Id).HasColumnName("id");

        builder.Property(line => line.InvoiceId)
            .HasColumnName("invoice_id")
            .IsRequired();

        builder.Property(line => line.LineNumber)
            .HasColumnName("line_number");

        builder.Property(line => line.Designation)
            .HasColumnName("designation")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(line => line.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 3);

        builder.Property(line => line.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(line => line.VatRate)
            .HasColumnName("vat_rate")
            .HasPrecision(5, 2);

        builder.Property(line => line.LineTotalExclVat)
            .HasColumnName("line_total_excl_vat")
            .HasPrecision(18, 2);

        builder.Ignore(line => line.VatAmount);
        builder.Ignore(line => line.LineTotalInclVat);

        builder.HasIndex(line => line.InvoiceId)
            .HasDatabaseName("ix_invoice_lines_invoice_id");
    }
}
