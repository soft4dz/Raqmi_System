using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Sync;

namespace RaqmiSystem.Infrastructure.Sync;

public sealed class WorkstationConfiguration : IEntityTypeConfiguration<Workstation>
{
    public void Configure(EntityTypeBuilder<Workstation> builder)
    {
        builder.ToTable("workstations", "security");

        builder.HasKey(workstation => workstation.Id);

        // ValueGeneratedNever est porteur, pas decoratif : l'identifiant d'un poste est CHOISI PAR
        // LE CLIENT (un Guid persiste dans son fichier de reglages), et AuditableEntity initialise
        // deja Id avec Guid.NewGuid(). Sans cette ligne EF considere la valeur comme deja generee
        // par la base, marque l'entite Modified au lieu de Added, et le premier battement d'un
        // poste inconnu leve DbUpdateConcurrencyException au lieu de creer la ligne.
        builder.Property(workstation => workstation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(workstation => workstation.Label)
            .HasColumnName("label")
            .HasMaxLength(Workstation.LabelMaxLength)
            .IsRequired();

        builder.Property(workstation => workstation.LastUserName)
            .HasColumnName("last_user_name")
            .HasMaxLength(Workstation.UserNameMaxLength)
            .IsRequired();

        builder.Property(workstation => workstation.AppVersion)
            .HasColumnName("app_version")
            .HasMaxLength(Workstation.AppVersionMaxLength)
            .IsRequired();

        builder.Property(workstation => workstation.LastHotelUnitCode)
            .HasColumnName("last_hotel_unit_code")
            .HasMaxLength(Workstation.HotelUnitCodeMaxLength);

        builder.Property(workstation => workstation.LastSeenUtc)
            .HasColumnName("last_seen_utc")
            .IsRequired();

        builder.Property(workstation => workstation.CreatedAt).HasColumnName("created_at");
        builder.Property(workstation => workstation.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(workstation => workstation.UpdatedAt).HasColumnName("updated_at");
        builder.Property(workstation => workstation.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        // Le registre se lit par defaut sur une fenetre glissante de 30 jours : cet index sert ce
        // filtre et le tri. Il est nomme explicitement car deux HasIndex portant sur la meme
        // colonne fusionnent silencieusement dans ce depot.
        builder.HasIndex(workstation => workstation.LastSeenUtc, "ix_workstations_last_seen_utc")
            .HasDatabaseName("ix_workstations_last_seen_utc");
    }
}
