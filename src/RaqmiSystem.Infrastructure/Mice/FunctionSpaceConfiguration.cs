using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Mice;

/// <summary>
/// Les tables de ce module vont dans le schema EXISTANT "lodging" et non dans un schema "mice"
/// neuf : un nouveau schema sortirait du pg_dump quotidien tant que create-app-role.sql n'a pas ete
/// etendu ET rejoue en administrateur sur le serveur. Une table hors sauvegarde est un risque bien
/// reel pour un gain de rangement nul.
/// </summary>
public sealed class FunctionSpaceConfiguration : IEntityTypeConfiguration<FunctionSpace>
{
    public void Configure(EntityTypeBuilder<FunctionSpace> builder)
    {
        builder.ToTable("function_spaces", "lodging", table =>
        {
            table.HasCheckConstraint("ck_function_spaces_capacity", "max_attendance > 0");
            table.HasCheckConstraint(
                "ck_function_spaces_area",
                "area_square_meters IS NULL OR CAST(area_square_meters AS numeric) > 0");
        });

        builder.HasKey(space => space.Id);

        builder.Property(space => space.Id).HasColumnName("id");

        builder.Property(space => space.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(space => space.Code)
            .HasColumnName("code")
            .HasMaxLength(FunctionSpace.CodeMaxLength)
            .IsRequired();

        builder.Property(space => space.Label)
            .HasColumnName("label")
            .HasMaxLength(FunctionSpace.LabelMaxLength)
            .IsRequired();

        builder.Property(space => space.MaxAttendance)
            .HasColumnName("max_attendance")
            .IsRequired();

        builder.Property(space => space.AreaSquareMeters)
            .HasColumnName("area_square_meters")
            .HasPrecision(10, 2);

        builder.Property(space => space.Notes)
            .HasColumnName("notes")
            .HasMaxLength(FunctionSpace.NotesMaxLength);

        builder.Property(space => space.IsActive).HasColumnName("is_active");

        builder.Property(space => space.CreatedAt).HasColumnName("created_at");
        builder.Property(space => space.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(space => space.UpdatedAt).HasColumnName("updated_at");
        builder.Property(space => space.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        // Un code d'espace est unique PAR UNITE : deux hotels du groupe peuvent tous deux avoir une
        // "SALLE1". L'index est nomme explicitement, deux HasIndex portant sur les memes colonnes
        // fusionnant silencieusement dans ce depot.
        builder.HasIndex(space => new { space.HotelUnitCode, space.Code }, "ux_function_spaces_unit_code")
            .IsUnique()
            .HasDatabaseName("ux_function_spaces_unit_code");
    }
}
