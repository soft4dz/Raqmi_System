using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Closing;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Closing;

/// <summary>
/// Discovered automatically by ApplyConfigurationsFromAssembly - no manual registration
/// required in RaqmiDbContext.
/// </summary>
public sealed class DailyClosingConfiguration : IEntityTypeConfiguration<DailyClosing>
{
    public void Configure(EntityTypeBuilder<DailyClosing> builder)
    {
        builder.ToTable("daily_closings", "exploitation", table =>
        {
            table.HasCheckConstraint(
                "ck_daily_closings_status",
                "status IN ('Closed', 'Reopened')");
        });

        builder.HasKey(closing => closing.Id);

        builder.Property(closing => closing.Id).HasColumnName("id");
        builder.Property(closing => closing.CreatedAt).HasColumnName("created_at");
        builder.Property(closing => closing.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(closing => closing.UpdatedAt).HasColumnName("updated_at");
        builder.Property(closing => closing.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(closing => closing.BusinessDate)
            .HasColumnName("business_date");

        builder.Property(closing => closing.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(closing => closing.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(closing => closing.ClosedAt)
            .HasColumnName("closed_at");

        builder.Property(closing => closing.ClosedBy)
            .HasColumnName("closed_by")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(closing => closing.ReopenedAt)
            .HasColumnName("reopened_at");

        builder.Property(closing => closing.ReopenedBy)
            .HasColumnName("reopened_by")
            .HasMaxLength(160);

        builder.Property(closing => closing.ReopenReason)
            .HasColumnName("reopen_reason")
            .HasMaxLength(500);

        builder.Property(closing => closing.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Ignore(closing => closing.IsClosed);

        builder.HasIndex(closing => new { closing.BusinessDate, closing.HotelUnitCode })
            .HasDatabaseName("ix_daily_closings_business_date_hotel_unit_code")
            .IsUnique();

        builder.HasIndex(closing => closing.Status)
            .HasDatabaseName("ix_daily_closings_status");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(closing => closing.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
