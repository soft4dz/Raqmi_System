using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;
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

        builder.Property(revenue => revenue.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(revenue => revenue.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(revenue => revenue.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(revenue => revenue.SubmittedBy).HasColumnName("submitted_by").HasMaxLength(160);
        builder.Property(revenue => revenue.ValidatedAt).HasColumnName("validated_at");
        builder.Property(revenue => revenue.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);
        builder.Property(revenue => revenue.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);

        builder.Ignore(revenue => revenue.Total);
        builder.Ignore(revenue => revenue.CanEdit);

        builder.HasIndex(revenue => new { revenue.BusinessDate, revenue.HotelUnitCode })
            .IsUnique();

        builder.HasIndex(revenue => revenue.Status);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(revenue => revenue.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
