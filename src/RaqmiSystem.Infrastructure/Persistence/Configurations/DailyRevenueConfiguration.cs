using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class DailyRevenueConfiguration : IEntityTypeConfiguration<DailyRevenue>
{
    public void Configure(EntityTypeBuilder<DailyRevenue> builder)
    {
        builder.ToTable("daily_revenues", "exploitation");

        builder.HasKey(revenue => revenue.Id);

        builder.Property(revenue => revenue.HotelUnitCode)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(revenue => revenue.Accommodation)
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Food)
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Beverage)
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Other)
            .HasPrecision(18, 2);

        builder.HasIndex(revenue => new { revenue.BusinessDate, revenue.HotelUnitCode })
            .IsUnique();
    }
}
