using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Infrastructure.Purchasing;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers", "purchasing", table =>
        {
            table.HasCheckConstraint(
                "ck_suppliers_supplier_type",
                "supplier_type IN ('Company', 'Individual', 'PublicEntity')");

            table.HasCheckConstraint(
                "ck_suppliers_nif_length",
                "nif IS NULL OR length(nif) = 15");
        });

        builder.HasKey(supplier => supplier.Id);

        builder.Property(supplier => supplier.Id).HasColumnName("id");
        builder.Property(supplier => supplier.CreatedAt).HasColumnName("created_at");
        builder.Property(supplier => supplier.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(supplier => supplier.UpdatedAt).HasColumnName("updated_at");
        builder.Property(supplier => supplier.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(supplier => supplier.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(supplier => supplier.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(supplier => supplier.SupplierType)
            .HasColumnName("supplier_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(supplier => supplier.Nif).HasColumnName("nif").HasMaxLength(15);
        builder.Property(supplier => supplier.Rc).HasColumnName("rc").HasMaxLength(20);
        builder.Property(supplier => supplier.Ai).HasColumnName("ai").HasMaxLength(20);
        builder.Property(supplier => supplier.Nis).HasColumnName("nis").HasMaxLength(20);
        builder.Property(supplier => supplier.Address).HasColumnName("address").HasMaxLength(200);
        builder.Property(supplier => supplier.City).HasColumnName("city").HasMaxLength(80);
        builder.Property(supplier => supplier.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(supplier => supplier.Email).HasColumnName("email").HasMaxLength(160);

        builder.Property(supplier => supplier.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: PurchaseOrderConfiguration's
        // HasPrincipalKey(supplier => supplier.Code) already forces EF to create an
        // alternate-key unique constraint on this column, exactly like CustomerConfiguration
        // relies on InvoiceConfiguration's alternate key.
        builder.HasIndex(supplier => supplier.Name)
            .HasDatabaseName("ix_suppliers_name");

        builder.HasIndex(supplier => supplier.IsActive)
            .HasDatabaseName("ix_suppliers_is_active");
    }
}
