using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Organization;
using RaqmiSystem.Domain.Revenue;

namespace RaqmiSystem.Infrastructure.Persistence.Configurations;

public sealed class DailyRevenueConfiguration : IEntityTypeConfiguration<DailyRevenue>
{
    public void Configure(EntityTypeBuilder<DailyRevenue> builder)
    {
        builder.ToTable("daily_revenues", "exploitation", table =>
        {
            table.HasCheckConstraint(
                "ck_daily_revenues_amounts_non_negative",
                "accommodation >= 0 AND food >= 0 AND beverage >= 0 AND other_revenue >= 0");

            table.HasCheckConstraint(
                "ck_daily_revenues_status",
                "status IN ('Draft', 'Submitted', 'Validated', 'Rejected')");
        });

        builder.HasKey(revenue => revenue.Id);

        builder.Property(revenue => revenue.Id).HasColumnName("id");
        builder.Property(revenue => revenue.CreatedAt).HasColumnName("created_at");
        builder.Property(revenue => revenue.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(revenue => revenue.UpdatedAt).HasColumnName("updated_at");
        builder.Property(revenue => revenue.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(revenue => revenue.BusinessDate)
            .HasColumnName("business_date");

        builder.Property(revenue => revenue.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        // Les quatre colonnes historiques sont conservées comme PROJECTION des lignes (voir
        // DailyRevenue) : la recette les recalcule à chaque changement de lignes et rien d'autre
        // ne les écrit. Elles restent en place pour que les lecteurs SQL non encore migrés (KPI,
        // pilotage, états) continuent d'obtenir les mêmes chiffres ; la source de vérité est
        // daily_revenue_lines. Elles pourront être retirées quand plus aucun lecteur ne les projette.
        builder.Property(revenue => revenue.Accommodation)
            .HasColumnName("accommodation")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Food)
            .HasColumnName("food")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Beverage)
            .HasColumnName("beverage")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Other)
            .HasColumnName("other_revenue")
            .HasPrecision(18, 2);

        builder.Property(revenue => revenue.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(revenue => revenue.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(revenue => revenue.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(revenue => revenue.SubmittedBy).HasColumnName("submitted_by").HasMaxLength(160);
        builder.Property(revenue => revenue.ValidatedAt).HasColumnName("validated_at");
        builder.Property(revenue => revenue.ValidatedBy).HasColumnName("validated_by").HasMaxLength(160);
        builder.Property(revenue => revenue.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);

        builder.Ignore(revenue => revenue.Total);
        builder.Ignore(revenue => revenue.CanEdit);

        builder.HasIndex(revenue => new { revenue.BusinessDate, revenue.HotelUnitCode })
            .IsUnique();

        builder.HasIndex(revenue => revenue.Status);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(revenue => revenue.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(revenue => revenue.Lines)
            .WithOne()
            .HasForeignKey(line => line.DailyRevenueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines est une projection en lecture seule du champ _lines : EF doit remplir le champ.
        // AutoInclude : les modules voisins chargent des recettes entières sans connaître les
        // lignes ; les leur livrer d'office rend Lines fiable partout, et ne coûte rien aux
        // requêtes qui projettent des colonnes (un Select ignore les inclusions).
        builder.Navigation(revenue => revenue.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
