using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kpi;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// Les bornes de pilotage configurees par l'etablissement. C'est l'une des trois seules tables
/// que le module KPI possede : il ne stocke aucune donnee metier, il lit celle des autres
/// modules.
/// </summary>
public sealed class KpiThresholdConfiguration : IEntityTypeConfiguration<KpiThreshold>
{
    public void Configure(EntityTypeBuilder<KpiThreshold> builder)
    {
        builder.ToTable("kpi_thresholds", "kpi");

        builder.HasKey(threshold => threshold.Id);

        builder.Property(threshold => threshold.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(threshold => threshold.CreatedAt).HasColumnName("created_at");
        builder.Property(threshold => threshold.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(threshold => threshold.UpdatedAt).HasColumnName("updated_at");
        builder.Property(threshold => threshold.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(threshold => threshold.KpiCode)
            .HasColumnName("kpi_code")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(threshold => threshold.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40);

        builder.Property(threshold => threshold.ScopeKey)
            .HasColumnName("scope_key")
            .HasMaxLength(40)
            .IsRequired();

        // Quatre decimales : certains ratios se pilotent au millieme, et une valeur tronquee en
        // base ne serait plus le seuil valide a l'ecran.
        builder.Property(threshold => threshold.FavorableThreshold)
            .HasColumnName("favorable_threshold")
            .HasPrecision(18, 4);

        builder.Property(threshold => threshold.CriticalThreshold)
            .HasColumnName("critical_threshold")
            .HasPrecision(18, 4);

        builder.Property(threshold => threshold.TargetValue)
            .HasColumnName("target_value")
            .HasPrecision(18, 4);

        builder.Property(threshold => threshold.OwnerRole).HasColumnName("owner_role").HasMaxLength(80);
        builder.Property(threshold => threshold.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(threshold => threshold.IsActive).HasColumnName("is_active");


        // UNE seule regle par couple (indicateur, perimetre). Deux regles concurrentes sur le
        // meme couple rendraient le verdict dependant de l'ordre de lecture, ce qu'aucun
        // utilisateur ne pourrait comprendre ni corriger.
        //
        // L'index porte la CLE DE PERIMETRE et non le code d'unite nullable : PostgreSQL comme
        // SQLite considerent deux NULL comme distincts dans un index unique, si bien qu'un index
        // sur le code laisserait passer autant de regles GROUPE concurrentes qu'on veut. Voir
        // Domain.Kpi.KpiScopeKey.
        builder.HasIndex(threshold => new { threshold.KpiCode, threshold.ScopeKey })
            .IsUnique()
            .HasDatabaseName("ux_kpi_thresholds_code_scope");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(threshold => threshold.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
