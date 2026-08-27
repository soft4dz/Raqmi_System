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

        builder.Property(unit => unit.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(unit => unit.Name)
            .HasMaxLength(160)
            .IsRequired();

        builder.HasIndex(unit => unit.Code)
            .IsUnique();
    }
}
