using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Sync;

namespace RaqmiSystem.Infrastructure.Sync;

public sealed class WorkstationFailureConfiguration : IEntityTypeConfiguration<WorkstationFailure>
{
    public void Configure(EntityTypeBuilder<WorkstationFailure> builder)
    {
        builder.ToTable("workstation_failures", "audit");

        builder.HasKey(failure => failure.Id);

        // Meme raison que pour Workstation, avec un role supplementaire : l'identifiant EST la cle
        // de deduplication. Le poste genere un identifiant d'evenement, donc un lot renvoye apres
        // une reponse perdue retombe sur la meme ligne au lieu d'en creer une seconde. La cle
        // primaire suffit a garantir cette unicite ; aucun index unique supplementaire n'est utile.
        builder.Property(failure => failure.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(failure => failure.WorkstationId)
            .HasColumnName("workstation_id")
            .IsRequired();

        builder.Property(failure => failure.Method)
            .HasColumnName("method")
            .HasMaxLength(WorkstationFailure.MethodMaxLength)
            .IsRequired();

        builder.Property(failure => failure.Path)
            .HasColumnName("path")
            .HasMaxLength(WorkstationFailure.PathMaxLength)
            .IsRequired();

        builder.Property(failure => failure.StatusCode).HasColumnName("status_code");

        builder.Property(failure => failure.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(failure => failure.Message)
            .HasColumnName("message")
            .HasMaxLength(WorkstationFailure.MessageMaxLength)
            .IsRequired();

        builder.Property(failure => failure.ClaimedAtUtc)
            .HasColumnName("claimed_at_utc")
            .IsRequired();

        builder.Property(failure => failure.RecordedAtUtc)
            .HasColumnName("recorded_at_utc")
            .IsRequired();

        builder.Property(failure => failure.ClockDriftSeconds)
            .HasColumnName("clock_drift_seconds")
            .IsRequired();

        builder.Property(failure => failure.CreatedAt).HasColumnName("created_at");
        builder.Property(failure => failure.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(failure => failure.UpdatedAt).HasColumnName("updated_at");
        builder.Property(failure => failure.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        // Le journal se lit du plus recent au plus ancien : cet index sert ce tri.
        builder.HasIndex(failure => failure.RecordedAtUtc, "ix_workstation_failures_recorded_at_utc")
            .HasDatabaseName("ix_workstation_failures_recorded_at_utc");

        // Et celui-ci sert la jointure vers le poste, ainsi qu'un futur filtre par poste.
        builder.HasIndex(failure => failure.WorkstationId, "ix_workstation_failures_workstation_id")
            .HasDatabaseName("ix_workstation_failures_workstation_id");
    }
}
