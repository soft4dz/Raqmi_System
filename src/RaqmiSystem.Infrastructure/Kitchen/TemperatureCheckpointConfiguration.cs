using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Infrastructure.Kitchen;

public sealed class TemperatureCheckpointConfiguration : IEntityTypeConfiguration<TemperatureCheckpoint>
{
    public void Configure(EntityTypeBuilder<TemperatureCheckpoint> builder)
    {
        builder.ToTable("temperature_checkpoints", "kitchen", table =>
        {
            // CAST for the SQLite test provider's TEXT-stored decimals - same pattern as
            // BudgetLineConfiguration.
            table.HasCheckConstraint(
                "ck_temperature_checkpoints_range",
                "CAST(min_temp AS numeric) < CAST(max_temp AS numeric)");
        });

        builder.HasKey(checkpoint => checkpoint.Id);

        builder.Property(checkpoint => checkpoint.Id).HasColumnName("id");
        builder.Property(checkpoint => checkpoint.CreatedAt).HasColumnName("created_at");
        builder.Property(checkpoint => checkpoint.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(checkpoint => checkpoint.UpdatedAt).HasColumnName("updated_at");
        builder.Property(checkpoint => checkpoint.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(checkpoint => checkpoint.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(checkpoint => checkpoint.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        // numeric(6,1): Celsius with one decimal place, the precision of a kitchen probe -
        // the domain refuses finer values upfront (TemperatureCheckpoint.RequireCelsius).
        builder.Property(checkpoint => checkpoint.MinTemp)
            .HasColumnName("min_temp")
            .HasPrecision(6, 1);

        builder.Property(checkpoint => checkpoint.MaxTemp)
            .HasColumnName("max_temp")
            .HasPrecision(6, 1);

        builder.Property(checkpoint => checkpoint.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(checkpoint => checkpoint.Code)
            .IsUnique()
            .HasDatabaseName("ux_temperature_checkpoints_code");
    }
}
