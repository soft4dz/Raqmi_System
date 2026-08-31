using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class SatisfactionEntryConfiguration : IEntityTypeConfiguration<SatisfactionEntry>
{
    public void Configure(EntityTypeBuilder<SatisfactionEntry> builder)
    {
        builder.ToTable("satisfaction_entries", "crm", table =>
        {
            // The NPS scale, restated where the answers land: a score outside 0-10 would silently
            // shift the promoter/detractor split of every period it appears in.
            table.HasCheckConstraint("ck_satisfaction_entries_score", "score BETWEEN 0 AND 10");

            table.HasCheckConstraint(
                "ck_satisfaction_entries_source",
                "source IN ('InRoom', 'Email', 'FrontDesk', 'Online', 'Phone')");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");
        builder.Property(entry => entry.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(entry => entry.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entry => entry.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(entry => entry.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entry => entry.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(entry => entry.SurveyDate).HasColumnName("survey_date");
        builder.Property(entry => entry.Score).HasColumnName("score");

        builder.Property(entry => entry.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(entry => entry.ReservationId).HasColumnName("reservation_id");

        builder.Property(entry => entry.Comment)
            .HasColumnName("comment")
            .HasMaxLength(2000);

        // Derived from the score by the NPS cut-offs; storing it would let a row claim a family
        // its own score does not put it in.
        builder.Ignore(entry => entry.Category);

        // The NPS of a period is computed unit by unit over this index.
        builder.HasIndex(entry => new { entry.HotelUnitCode, entry.SurveyDate })
            .HasDatabaseName("ix_satisfaction_entries_hotel_unit_code_survey_date");

        builder.HasIndex(entry => entry.CustomerCode)
            .HasDatabaseName("ix_satisfaction_entries_customer_code");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(entry => entry.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(entry => entry.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Reservation>()
            .WithMany()
            .HasForeignKey(entry => entry.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
