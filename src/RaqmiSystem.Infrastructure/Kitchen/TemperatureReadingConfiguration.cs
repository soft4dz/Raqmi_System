using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kitchen;

namespace RaqmiSystem.Infrastructure.Kitchen;

public sealed class TemperatureReadingConfiguration : IEntityTypeConfiguration<TemperatureReading>
{
    public void Configure(EntityTypeBuilder<TemperatureReading> builder)
    {
        builder.ToTable("temperature_readings", "kitchen", table =>
        {
            // HACCP: a non-compliant reading must carry a corrective action. The bare boolean
            // column works as a condition on both providers (PostgreSQL boolean, SQLite 0/1).
            table.HasCheckConstraint(
                "ck_temperature_readings_corrective_action",
                "is_compliant OR corrective_action IS NOT NULL");
        });

        builder.HasKey(reading => reading.Id);

        builder.Property(reading => reading.Id).HasColumnName("id");
        builder.Property(reading => reading.CreatedAt).HasColumnName("created_at");
        builder.Property(reading => reading.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(reading => reading.UpdatedAt).HasColumnName("updated_at");
        builder.Property(reading => reading.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(reading => reading.CheckpointCode)
            .HasColumnName("checkpoint_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reading => reading.MeasuredAt)
            .HasColumnName("measured_at");

        builder.Property(reading => reading.ValueCelsius)
            .HasColumnName("value_celsius")
            .HasPrecision(6, 1);

        builder.Property(reading => reading.RecordedBy)
            .HasColumnName("recorded_by")
            .HasMaxLength(160)
            .IsRequired();

        // Frozen thresholds: what the compliance verdict was judged against at the moment of
        // the reading. A later checkpoint edit never rewrites these columns - same snapshot
        // logic as the customer/issuer columns of finance.invoices.
        builder.Property(reading => reading.MinTempSnapshot)
            .HasColumnName("min_temp_snapshot")
            .HasPrecision(6, 1);

        builder.Property(reading => reading.MaxTempSnapshot)
            .HasColumnName("max_temp_snapshot")
            .HasPrecision(6, 1);

        builder.Property(reading => reading.IsCompliant)
            .HasColumnName("is_compliant");

        builder.Property(reading => reading.CorrectiveAction)
            .HasColumnName("corrective_action")
            .HasMaxLength(500);

        builder.HasIndex(reading => reading.MeasuredAt)
            .HasDatabaseName("ix_temperature_readings_measured_at");

        builder.HasIndex(reading => reading.CheckpointCode)
            .HasDatabaseName("ix_temperature_readings_checkpoint_code");

        builder.HasIndex(reading => reading.IsCompliant)
            .HasDatabaseName("ix_temperature_readings_is_compliant");

        builder.HasOne<TemperatureCheckpoint>()
            .WithMany()
            .HasPrincipalKey(checkpoint => checkpoint.Code)
            .HasForeignKey(reading => reading.CheckpointCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
