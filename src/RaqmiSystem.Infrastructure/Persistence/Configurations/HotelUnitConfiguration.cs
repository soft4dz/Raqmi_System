using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class HotelUnitConfiguration : IEntityTypeConfiguration<HotelUnit>
{
    public void Configure(EntityTypeBuilder<HotelUnit> builder)
    {
        builder.ToTable("hotel_units", "organization");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id).HasColumnName("id");
        builder.Property(unit => unit.CreatedAt).HasColumnName("created_at");
        builder.Property(unit => unit.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(unit => unit.UpdatedAt).HasColumnName("updated_at");
        builder.Property(unit => unit.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(unit => unit.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(unit => unit.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(unit => unit.UnitType)
            .HasColumnName("unit_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(unit => unit.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(unit => unit.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(unit => unit.Code)
            .IsUnique();

        builder.HasIndex(unit => unit.DisplayOrder);
    }
}
