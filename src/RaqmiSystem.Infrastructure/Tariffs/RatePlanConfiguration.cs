using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Tariffs;

namespace RaqmiSystem.Infrastructure.Tariffs;

public sealed class RatePlanConfiguration : IEntityTypeConfiguration<RatePlan>
{
    public void Configure(EntityTypeBuilder<RatePlan> builder)
    {
        builder.ToTable("rate_plans", "tariffs");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Id).HasColumnName("id");
        builder.Property(plan => plan.CreatedAt).HasColumnName("created_at");
        builder.Property(plan => plan.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(plan => plan.UpdatedAt).HasColumnName("updated_at");
        builder.Property(plan => plan.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(plan => plan.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(plan => plan.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(plan => plan.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(plan => plan.IsDefault)
            .HasColumnName("is_default");

        builder.Property(plan => plan.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(plan => plan.Code)
            .IsUnique()
            .HasDatabaseName("ux_rate_plans_code");

        // THE invariant of the module's plan side: at most one default ACTIVE plan per unit.
        // The filter deliberately only constrains rows where is_default AND is_active, so
        // deactivated plans keep their flag as dormant history without blocking a new default.
        // Bare boolean columns (no "= 1"/"= true") are the one spelling both providers accept:
        // PostgreSQL has real booleans, and SQLite (integration-test provider) evaluates the
        // truthiness of its 0/1 integers.
        //
        // The two indexes below share the same property set, and EF Core MERGES two unnamed
        // HasIndex calls over identical properties into a single index (the WaveHotel migration
        // shipped exactly one merged index because of that). Each call therefore carries its own
        // model-level index name (the second HasIndex argument): that is what keeps them two
        // distinct indexes in the model and in the generated schema.
        builder.HasIndex(plan => plan.HotelUnitCode, "ux_rate_plans_default_per_unit")
            .IsUnique()
            .HasFilter("is_default AND is_active")
            .HasDatabaseName("ux_rate_plans_default_per_unit");

        // Plain non-unique index supporting FK lookups by unit: the filtered index above only
        // covers default+active rows, so it cannot serve that purpose.
        builder.HasIndex(plan => plan.HotelUnitCode, "ix_rate_plans_hotel_unit_code")
            .HasDatabaseName("ix_rate_plans_hotel_unit_code");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(plan => plan.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
