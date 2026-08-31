using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Mice;

namespace RaqmiSystem.Infrastructure.Mice;

public sealed class EventScheduleItemConfiguration : IEntityTypeConfiguration<EventScheduleItem>
{
    public void Configure(EntityTypeBuilder<EventScheduleItem> builder)
    {
        builder.ToTable("event_schedule_items", "lodging");

        builder.HasKey(item => item.Id);

        // Meme raison que pour EventBookingLine : identifiant auto-attribue, donc EF doit etre
        // explicitement empeche de le prendre pour une valeur generee par la base.
        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.EventBookingId).HasColumnName("event_booking_id");

        builder.Property(item => item.StartTime).HasColumnName("start_time");

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(EventScheduleItem.DescriptionMaxLength)
            .IsRequired();

        builder.Property(item => item.Department)
            .HasColumnName("department")
            .HasMaxLength(EventScheduleItem.DepartmentMaxLength);

        builder.HasIndex(item => item.EventBookingId, "ix_event_schedule_items_booking")
            .HasDatabaseName("ix_event_schedule_items_booking");
    }
}
