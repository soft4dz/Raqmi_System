using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Billing;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_invoices_status",
                "status IN ('Draft', 'Issued', 'Paid', 'Cancelled')");
        });

        builder.HasKey(invoice => invoice.Id);

        builder.Property(invoice => invoice.Id).HasColumnName("id");
        builder.Property(invoice => invoice.CreatedAt).HasColumnName("created_at");
        builder.Property(invoice => invoice.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(invoice => invoice.UpdatedAt).HasColumnName("updated_at");
        builder.Property(invoice => invoice.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(invoice => invoice.Number)
            .HasColumnName("number")
            .HasMaxLength(30);

        builder.Property(invoice => invoice.IssuedYear)
            .HasColumnName("issued_year");

        builder.Property(invoice => invoice.IssuedSequence)
            .HasColumnName("issued_sequence");

        builder.Property(invoice => invoice.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(invoice => invoice.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(invoice => invoice.InvoiceDate)
            .HasColumnName("invoice_date");

        builder.Property(invoice => invoice.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(invoice => invoice.TotalExclVat)
            .HasColumnName("total_excl_vat")
            .HasPrecision(18, 2);

        builder.Property(invoice => invoice.TotalVat)
            .HasColumnName("total_vat")
            .HasPrecision(18, 2);

        builder.Property(invoice => invoice.TotalInclVat)
            .HasColumnName("total_incl_vat")
            .HasPrecision(18, 2);

        // Legal immutability of issued invoices: the customer's identification frozen at
        // issue time (null while the invoice is a Draft). Lengths mirror the Customer columns.
        builder.Property(invoice => invoice.CustomerNameSnapshot)
            .HasColumnName("customer_name_snapshot")
            .HasMaxLength(200);

        builder.Property(invoice => invoice.CustomerNifSnapshot)
            .HasColumnName("customer_nif_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.CustomerRcSnapshot)
            .HasColumnName("customer_rc_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.CustomerAiSnapshot)
            .HasColumnName("customer_ai_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.CustomerNisSnapshot)
            .HasColumnName("customer_nis_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.CustomerAddressSnapshot)
            .HasColumnName("customer_address_snapshot")
            .HasMaxLength(200);

        // Identity of the emitter frozen at issue time, read from settings.application_settings.
        // Lengths mirror the ApplicationSettings company columns.
        builder.Property(invoice => invoice.IssuerNameSnapshot)
            .HasColumnName("issuer_name_snapshot")
            .HasMaxLength(200);

        builder.Property(invoice => invoice.IssuerNifSnapshot)
            .HasColumnName("issuer_nif_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.IssuerRcSnapshot)
            .HasColumnName("issuer_rc_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.IssuerAiSnapshot)
            .HasColumnName("issuer_ai_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.IssuerNisSnapshot)
            .HasColumnName("issuer_nis_snapshot")
            .HasMaxLength(20);

        builder.Property(invoice => invoice.IssuerAddressSnapshot)
            .HasColumnName("issuer_address_snapshot")
            .HasMaxLength(200);

        builder.Property(invoice => invoice.IssuedAt).HasColumnName("issued_at");
        builder.Property(invoice => invoice.IssuedBy).HasColumnName("issued_by").HasMaxLength(160);
        builder.Property(invoice => invoice.PaidAt).HasColumnName("paid_at");
        builder.Property(invoice => invoice.PaidBy).HasColumnName("paid_by").HasMaxLength(160);
        builder.Property(invoice => invoice.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(invoice => invoice.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(invoice => invoice.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);

        builder.Ignore(invoice => invoice.CanEdit);

        // Drafts carry NULL number/year/sequence; both PostgreSQL and SQLite treat NULLs as
        // distinct in unique indexes, so only issued invoices are constrained.
        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("ux_invoices_number");

        // The concurrency guard behind the issue-time number allocation: two concurrent issues
        // computing the same next sequence collide here and one of them retries.
        builder.HasIndex(invoice => new { invoice.IssuedYear, invoice.IssuedSequence })
            .IsUnique()
            .HasDatabaseName("ux_invoices_issued_year_sequence");

        builder.HasIndex(invoice => invoice.Status)
            .HasDatabaseName("ix_invoices_status");

        builder.HasIndex(invoice => invoice.InvoiceDate)
            .HasDatabaseName("ix_invoices_invoice_date");

        builder.HasIndex(invoice => invoice.CustomerCode)
            .HasDatabaseName("ix_invoices_customer_code");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(invoice => invoice.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(invoice => invoice.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey(line => line.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is an IReadOnlyCollection backed by the _lines field; EF must mutate the field,
        // never the read-only projection.
        builder.Navigation(invoice => invoice.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
