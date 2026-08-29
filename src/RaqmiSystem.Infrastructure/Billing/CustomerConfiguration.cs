using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;

namespace RaqmiSystem.Infrastructure.Billing;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_customers_customer_type",
                "customer_type IN ('Company', 'Individual', 'PublicEntity')");

            table.HasCheckConstraint(
                "ck_customers_nif_length",
                "nif IS NULL OR length(nif) = 15");
        });

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id).HasColumnName("id");
        builder.Property(customer => customer.CreatedAt).HasColumnName("created_at");
        builder.Property(customer => customer.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(customer => customer.UpdatedAt).HasColumnName("updated_at");
        builder.Property(customer => customer.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(customer => customer.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(customer => customer.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(customer => customer.CustomerType)
            .HasColumnName("customer_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(customer => customer.Nif).HasColumnName("nif").HasMaxLength(15);
        builder.Property(customer => customer.Rc).HasColumnName("rc").HasMaxLength(20);
        builder.Property(customer => customer.Ai).HasColumnName("ai").HasMaxLength(20);
        builder.Property(customer => customer.Nis).HasColumnName("nis").HasMaxLength(20);
        builder.Property(customer => customer.Address).HasColumnName("address").HasMaxLength(200);
        builder.Property(customer => customer.City).HasColumnName("city").HasMaxLength(80);
        builder.Property(customer => customer.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(customer => customer.Email).HasColumnName("email").HasMaxLength(160);

        builder.Property(customer => customer.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: InvoiceConfiguration's HasPrincipalKey(customer => customer.Code)
        // already forces EF to create an alternate-key unique constraint on this column, exactly like
        // HotelUnitConfiguration relies on DailyRevenueConfiguration's alternate key.
        builder.HasIndex(customer => customer.Name)
            .HasDatabaseName("ix_customers_name");

        builder.HasIndex(customer => customer.IsActive)
            .HasDatabaseName("ix_customers_is_active");
    }
}
