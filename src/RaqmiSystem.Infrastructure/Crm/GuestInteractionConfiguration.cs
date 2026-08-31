using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Crm;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class GuestInteractionConfiguration : IEntityTypeConfiguration<GuestInteraction>
{
    public void Configure(EntityTypeBuilder<GuestInteraction> builder)
    {
        builder.ToTable("guest_interactions", "crm", table =>
        {
            table.HasCheckConstraint(
                "ck_guest_interactions_channel",
                "channel IN ('Phone', 'Email', 'Sms', 'InPerson', 'Web')");

            table.HasCheckConstraint(
                "ck_guest_interactions_direction",
                "direction IN ('Inbound', 'Outbound')");
        });

        builder.HasKey(interaction => interaction.Id);

        builder.Property(interaction => interaction.Id).HasColumnName("id");
        builder.Property(interaction => interaction.CreatedAt).HasColumnName("created_at");
        builder.Property(interaction => interaction.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(interaction => interaction.UpdatedAt).HasColumnName("updated_at");
        builder.Property(interaction => interaction.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(interaction => interaction.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(interaction => interaction.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40);

        builder.Property(interaction => interaction.OccurredAt).HasColumnName("occurred_at");

        builder.Property(interaction => interaction.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(interaction => interaction.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(interaction => interaction.Subject)
            .HasColumnName("subject")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(interaction => interaction.HandledBy)
            .HasColumnName("handled_by")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(interaction => interaction.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        // The timeline of one guest, most recent first, is the only way this table is read.
        builder.HasIndex(interaction => new { interaction.CustomerCode, interaction.OccurredAt })
            .HasDatabaseName("ix_guest_interactions_customer_code_occurred_at");

        builder.HasIndex(interaction => interaction.HotelUnitCode)
            .HasDatabaseName("ix_guest_interactions_hotel_unit_code");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(interaction => interaction.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(interaction => interaction.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
