using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class YieldRuleConfiguration : IEntityTypeConfiguration<YieldRule>
{
    public void Configure(EntityTypeBuilder<YieldRule> builder)
    {
        builder.ToTable("yield_rules", "lodging", table =>
        {
            table.HasCheckConstraint("ck_yield_rules_dates", "to_date >= from_date");

            table.HasCheckConstraint(
                "ck_yield_rules_trigger",
                "trigger_kind IN ('OccupancyAtOrAbove', 'OccupancyBelow', 'LeadTimeAtOrBelow', "
                + "'LeadTimeAtOrAbove', 'DayOfWeek', 'Always')");

            table.HasCheckConstraint(
                "ck_yield_rules_adjustment",
                "CAST(adjustment_percent AS numeric) <> 0 "
                + "AND CAST(adjustment_percent AS numeric) BETWEEN -300 AND 300");
        });

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Id).HasColumnName("id");
        builder.Property(rule => rule.CreatedAt).HasColumnName("created_at");
        builder.Property(rule => rule.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(rule => rule.UpdatedAt).HasColumnName("updated_at");
        builder.Property(rule => rule.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(rule => rule.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(rule => rule.Code)
            .HasColumnName("code")
            .HasMaxLength(YieldRule.CodeMaxLength)
            .IsRequired();

        builder.Property(rule => rule.Label)
            .HasColumnName("label")
            .HasMaxLength(YieldRule.LabelMaxLength)
            .IsRequired();

        builder.Property(rule => rule.RoomTypeCode).HasColumnName("room_type_code").HasMaxLength(40);
        builder.Property(rule => rule.RatePlanCode).HasColumnName("rate_plan_code").HasMaxLength(40);

        builder.Property(rule => rule.FromDate).HasColumnName("from_date");
        builder.Property(rule => rule.ToDate).HasColumnName("to_date");

        builder.Property(rule => rule.Trigger)
            .HasColumnName("trigger_kind")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(rule => rule.ThresholdValue).HasColumnName("threshold_value").HasPrecision(9, 2);

        builder.Property(rule => rule.DaysOfWeek).HasColumnName("days_of_week").HasMaxLength(60);

        builder.Property(rule => rule.AdjustmentPercent)
            .HasColumnName("adjustment_percent")
            .HasPrecision(6, 2);

        builder.Property(rule => rule.Priority).HasColumnName("priority");
        builder.Property(rule => rule.IsActive).HasColumnName("is_active");

        builder.Property(rule => rule.Notes)
            .HasColumnName("notes")
            .HasMaxLength(YieldRule.NotesMaxLength);

        builder.HasIndex(rule => new { rule.HotelUnitCode, rule.Code })
            .IsUnique()
            .HasDatabaseName("ux_yield_rules_hotel_unit_code_code");

        builder.HasIndex(rule => new { rule.HotelUnitCode, rule.FromDate, rule.ToDate })
            .HasDatabaseName("ix_yield_rules_unit_period");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(rule => rule.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
