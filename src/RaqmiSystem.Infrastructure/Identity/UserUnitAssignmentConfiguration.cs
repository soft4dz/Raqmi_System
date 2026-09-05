using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Identity;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Identity;

/// <summary>
/// Table <c>security.user_unit_assignments</c> (lot 2.2). Elle vit ici, a cote du service qui
/// l'administre, et non dans Persistence/Configurations : RaqmiDbContext decouvre toutes les
/// configurations de l'assembly (ApplyConfigurationsFromAssembly), le dossier n'a donc aucune
/// importance pour EF, et ce lot ne touche ni au contexte ni aux migrations - l'integrateur
/// ajoute le DbSet et genere la migration.
///
/// L'unicite (utilisateur, unite) est la regle metier : une meme unite ne s'affecte pas deux
/// fois au meme compte, et c'est l'index unique - pas le service - qui la tient sous
/// concurrence. Le code d'unite est une cle etrangere vers <c>organization.hotel_units.code</c>
/// (cle alternative deja creee pour les recettes journalieres) : une affectation ne peut
/// designer qu'une unite qui existe, et une unite affectee ne peut pas etre supprimee.
/// </summary>
public sealed class UserUnitAssignmentConfiguration : IEntityTypeConfiguration<UserUnitAssignment>
{
    public void Configure(EntityTypeBuilder<UserUnitAssignment> builder)
    {
        builder.ToTable("user_unit_assignments", "security");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id).HasColumnName("id");
        builder.Property(assignment => assignment.UserId).HasColumnName("user_id");

        builder.Property(assignment => assignment.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(assignment => assignment.AssignedAt).HasColumnName("assigned_at");

        builder.Property(assignment => assignment.AssignedBy)
            .HasColumnName("assigned_by")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(assignment => assignment.ValidFrom).HasColumnName("valid_from");
        builder.Property(assignment => assignment.ValidTo).HasColumnName("valid_to");

        builder.HasIndex(assignment => new { assignment.UserId, assignment.HotelUnitCode })
            .IsUnique();

        builder.HasIndex(assignment => assignment.HotelUnitCode);

        // Supprimer un compte emporte ses affectations (comme ses roles et ses jetons de
        // rafraichissement) ; supprimer une unite affectee est refuse : les unites se
        // desactivent, elles ne disparaissent pas.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasForeignKey(assignment => assignment.HotelUnitCode)
            .HasPrincipalKey(unit => unit.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
