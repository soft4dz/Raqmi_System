using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// Les valeurs historisees des indicateurs.
///
/// C'est la seule table du module qui grossit avec le temps, et elle est indexee pour les deux
/// seules facons dont on l'interroge : tracer la courbe d'un indicateur sur un perimetre (index
/// code + unite + debut de periode) et relire toute une periode d'un coup lors d'une cloture
/// (index sur les bornes). L'index unique sur (code, unite, bornes) est ce qui rend la pose
/// d'instantane idempotente : reposer la meme periode rafraichit la ligne existante au lieu
/// d'empiler des doublons dont personne ne saurait lequel fait foi.
/// </summary>
public sealed class KpiSnapshotConfiguration : IEntityTypeConfiguration<KpiSnapshot>
{
    public void Configure(EntityTypeBuilder<KpiSnapshot> builder)
    {
        builder.ToTable("kpi_snapshots", "kpi", table =>
        {
            table.HasCheckConstraint(
                "ck_kpi_snapshots_period",
                "period_end >= period_start");

            table.HasCheckConstraint(
                "ck_kpi_snapshots_status",
                "status IN ('Provisional', 'Closed')");

            table.HasCheckConstraint(
                "ck_kpi_snapshots_quality",
                "quality IN ('Valid', 'Partial', 'MissingData', 'NotApplicable')");
        });

        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(snapshot => snapshot.CreatedAt).HasColumnName("created_at");
        builder.Property(snapshot => snapshot.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(snapshot => snapshot.UpdatedAt).HasColumnName("updated_at");
        builder.Property(snapshot => snapshot.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(snapshot => snapshot.KpiCode)
            .HasColumnName("kpi_code")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(snapshot => snapshot.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40);

        builder.Property(snapshot => snapshot.ScopeKey)
            .HasColumnName("scope_key")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(snapshot => snapshot.PeriodStart).HasColumnName("period_start");
        builder.Property(snapshot => snapshot.PeriodEnd).HasColumnName("period_end");

        builder.Property(snapshot => snapshot.Granularity)
            .HasColumnName("granularity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Six decimales, contre deux pour un montant : un instantane conserve aussi des
        // numerateurs et denominateurs qui peuvent etre des quantites, et arrondir le
        // denominateur d'un ratio au centieme fausserait la reconsolidation d'un groupe.
        builder.Property(snapshot => snapshot.Value).HasColumnName("value").HasPrecision(20, 6);
        builder.Property(snapshot => snapshot.Numerator).HasColumnName("numerator").HasPrecision(20, 6);
        builder.Property(snapshot => snapshot.Denominator).HasColumnName("denominator").HasPrecision(20, 6);

        builder.Property(snapshot => snapshot.Quality)
            .HasColumnName("quality")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(snapshot => snapshot.FormulaVersion).HasColumnName("formula_version");
        builder.Property(snapshot => snapshot.CalculatedAt).HasColumnName("calculated_at");

        builder.Property(snapshot => snapshot.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(snapshot => snapshot.ClosedAt).HasColumnName("closed_at");
        builder.Property(snapshot => snapshot.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);

        builder.Ignore(snapshot => snapshot.IsClosed);

        // L'index porte la CLE DE PERIMETRE et non le code d'unite nullable : sans cela, deux
        // poses concurrentes de la meme periode GROUPE creeraient deux lignes et personne ne
        // saurait laquelle fait foi. Voir Domain.Kpi.KpiScopeKey.
        builder.HasIndex(snapshot => new
        {
            snapshot.KpiCode,
            snapshot.ScopeKey,
            snapshot.PeriodStart,
            snapshot.PeriodEnd
        })
            .IsUnique()
            .HasDatabaseName("ux_kpi_snapshots_code_scope_period");

        builder.HasIndex(snapshot => new { snapshot.PeriodStart, snapshot.PeriodEnd })
            .HasDatabaseName("ix_kpi_snapshots_period");

        builder.HasIndex(snapshot => snapshot.Status)
            .HasDatabaseName("ix_kpi_snapshots_status");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(snapshot => snapshot.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
