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

        builder.Property(revenue => revenue.Id).HasColumnName("id");
        builder.Property(revenue => revenue.CreatedAt).HasColumnName("created_at");
        builder.Property(revenue => revenue.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(revenue => revenue.UpdatedAt).HasColumnName("updated_at");
        builder.Property(revenue => revenue.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(revenue => revenue.BusinessDate)
            .HasColumnName("business_date");

        builder.Property(revenue => revenue.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(revenue => revenue.Accommodation)
            .HasColumnName("accommodation")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Food)
            .HasColumnName("food")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Beverage)
            .HasColumnName("beverage")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Other)
            .HasColumnName("other_revenue")
            .HasPrecision(18, 2);

        builder.HasIndex(revenue => new { revenue.BusinessDate, revenue.HotelUnitCode })
            .IsUnique();
    }
}
