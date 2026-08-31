using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Infrastructure.Purchasing;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", "purchasing", table =>
        {
            table.HasCheckConstraint(
                "ck_purchase_orders_status",
                "status IN ('Draft', 'Approved', 'PartiallyReceived', 'Received', 'Cancelled')");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimals as TEXT, and a bare numeric comparison would misbehave there.
            table.HasCheckConstraint(
                "ck_purchase_orders_total_positive",
                "CAST(total_excl_vat AS numeric) >= 0");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.Id).HasColumnName("id");
        builder.Property(order => order.CreatedAt).HasColumnName("created_at");
        builder.Property(order => order.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(order => order.UpdatedAt).HasColumnName("updated_at");
        builder.Property(order => order.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(order => order.Number)
            .HasColumnName("number")
            .HasMaxLength(30);

        builder.Property(order => order.ApprovedYear)
            .HasColumnName("approved_year");

        builder.Property(order => order.ApprovedSequence)
            .HasColumnName("approved_sequence");

        builder.Property(order => order.SupplierCode)
            .HasColumnName("supplier_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(order => order.WarehouseCode)
            .HasColumnName("warehouse_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(order => order.OrderDate)
            .HasColumnName("order_date");

        builder.Property(order => order.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(order => order.TotalExclVat)
            .HasColumnName("total_excl_vat")
            .HasPrecision(18, 2);

        builder.Property(order => order.ApprovedAt).HasColumnName("approved_at");
        builder.Property(order => order.ApprovedBy).HasColumnName("approved_by").HasMaxLength(160);
        builder.Property(order => order.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(order => order.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(order => order.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);

        builder.Ignore(order => order.CanEdit);
        builder.Ignore(order => order.CanReceive);
        builder.Ignore(order => order.HasAnyReceipt);

        // Drafts carry NULL number/year/sequence; both PostgreSQL and SQLite treat NULLs as
        // distinct in unique indexes, so only approved orders are constrained.
        builder.HasIndex(order => order.Number)
            .IsUnique()
            .HasDatabaseName("ux_purchase_orders_number");

        // The concurrency guard behind the approval-time number allocation: two concurrent
        // approvals computing the same next sequence collide here and one of them retries -
        // same mechanic as ux_invoices_issued_year_sequence.
        builder.HasIndex(order => new { order.ApprovedYear, order.ApprovedSequence })
            .IsUnique()
            .HasDatabaseName("ux_purchase_orders_approved_year_sequence");

        builder.HasIndex(order => order.Status)
            .HasDatabaseName("ix_purchase_orders_status");

        builder.HasIndex(order => order.OrderDate)
            .HasDatabaseName("ix_purchase_orders_order_date");

        builder.HasIndex(order => order.SupplierCode)
            .HasDatabaseName("ix_purchase_orders_supplier_code");

        builder.HasIndex(order => order.WarehouseCode)
            .HasDatabaseName("ix_purchase_orders_warehouse_code");

        builder.HasOne<Supplier>()
            .WithMany()
            .HasPrincipalKey(supplier => supplier.Code)
            .HasForeignKey(order => order.SupplierCode)
            .OnDelete(DeleteBehavior.Restrict);

        // No FK on warehouse_code on purpose: the warehouse referential belongs to the stock
        // module and is consumed through its published service contract only; reception is
        // where the stock module itself validates the warehouse.

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is an IReadOnlyCollection backed by the _lines field; EF must mutate the field,
        // never the read-only projection.
        builder.Navigation(order => order.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
