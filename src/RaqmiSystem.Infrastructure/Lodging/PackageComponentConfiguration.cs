using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class PackageComponentConfiguration : IEntityTypeConfiguration<PackageComponent>
{
    public void Configure(EntityTypeBuilder<PackageComponent> builder)
    {
        builder.ToTable("package_components", "lodging", table =>
        {
            table.HasCheckConstraint("ck_package_components_amount", "CAST(amount AS numeric) > 0");

            table.HasCheckConstraint(
                "ck_package_components_charge_kind",
                "charge_kind IN ('Night', 'Extra', 'Tax', 'Package')");
        });

        builder.HasKey(component => component.Id);

        builder.Property(component => component.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(component => component.PackageId).HasColumnName("package_id");

        builder.Property(component => component.Label)
            .HasColumnName("label")
            .HasMaxLength(PackageComponent.LabelMaxLength)
            .IsRequired();

        builder.Property(component => component.Amount).HasColumnName("amount").HasPrecision(18, 2);

        builder.Property(component => component.ChargeKind)
            .HasColumnName("charge_kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(component => component.ExtraCode)
            .HasColumnName("extra_code")
            .HasMaxLength(ExtraItem.CodeMaxLength);

        builder.Property(component => component.PricingBasis)
            .HasColumnName("pricing_basis")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(component => component.PackageId)
            .HasDatabaseName("ix_package_components_package_id");
    }
}
