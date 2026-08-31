using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class GuestProfileConfiguration : IEntityTypeConfiguration<GuestProfile>
{
    public void Configure(EntityTypeBuilder<GuestProfile> builder)
    {
        builder.ToTable("guest_profiles", "crm", table =>
        {
            // A granted consent always carries the date it was granted on - that date IS the
            // proof the establishment has to produce. A profile with no stamp at all is a guest
            // who was never asked, which the domain models as consent = false without a date.
            table.HasCheckConstraint(
                "ck_guest_profiles_consent_stamp",
                "NOT marketing_consent OR marketing_consent_updated_at IS NOT NULL");
        });

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id).HasColumnName("id");
        builder.Property(profile => profile.CreatedAt).HasColumnName("created_at");
        builder.Property(profile => profile.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(profile => profile.UpdatedAt).HasColumnName("updated_at");
        builder.Property(profile => profile.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(profile => profile.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(profile => profile.SegmentCode)
            .HasColumnName("segment_code")
            .HasMaxLength(40);

        builder.Property(profile => profile.PreferredLanguage)
            .HasColumnName("preferred_language")
            .HasMaxLength(10);

        builder.Property(profile => profile.BirthDate).HasColumnName("birth_date");

        builder.Property(profile => profile.Preferences)
            .HasColumnName("preferences")
            .HasMaxLength(600);

        builder.Property(profile => profile.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(profile => profile.IsVip).HasColumnName("is_vip");

        builder.Property(profile => profile.MarketingConsent).HasColumnName("marketing_consent");

        builder.Property(profile => profile.MarketingConsentUpdatedAt)
            .HasColumnName("marketing_consent_updated_at");

        // ONE profile per customer: the profile is the CRM half of a customer, not a history of
        // what was thought of them. Two concurrent first-saves race past the service
        // exists-check, collide here, and the loser surfaces as a retryable 409 instead of a
        // customer with two contradictory profiles.
        builder.HasIndex(profile => profile.CustomerCode)
            .IsUnique()
            .HasDatabaseName("ux_guest_profiles_customer_code");

        // Every campaign audience is resolved by segment.
        builder.HasIndex(profile => profile.SegmentCode)
            .HasDatabaseName("ix_guest_profiles_segment_code");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(profile => profile.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CustomerSegment>()
            .WithMany()
            .HasPrincipalKey(segment => segment.Code)
            .HasForeignKey(profile => profile.SegmentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
