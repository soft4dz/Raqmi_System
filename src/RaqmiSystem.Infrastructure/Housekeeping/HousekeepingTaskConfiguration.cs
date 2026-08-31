using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Housekeeping;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Housekeeping;

public sealed class HousekeepingTaskConfiguration : IEntityTypeConfiguration<HousekeepingTask>
{
    public void Configure(EntityTypeBuilder<HousekeepingTask> builder)
    {
        builder.ToTable("housekeeping_tasks", "housekeeping", table =>
        {
            table.HasCheckConstraint(
                "ck_housekeeping_tasks_status",
                "status IN ('Pending', 'InProgress', 'Cleaned', 'Inspected', 'Rejected', 'Cancelled')");

            table.HasCheckConstraint(
                "ck_housekeeping_tasks_task_type",
                "task_type IN ('Departure', 'Stayover', 'Vacant', 'DeepClean')");

            table.HasCheckConstraint(
                "ck_housekeeping_tasks_duration",
                "duration_minutes IS NULL OR duration_minutes >= 0");

            // A refusal without a reason is exactly what the domain refuses to build; the
            // database refuses to hold it too, so no import path can slip one in behind the
            // entity.
            table.HasCheckConstraint(
                "ck_housekeeping_tasks_rejection_reason",
                "(status <> 'Rejected') OR (inspection_notes IS NOT NULL)");

            table.HasCheckConstraint(
                "ck_housekeeping_tasks_cancel_reason",
                "(status <> 'Cancelled') OR (cancel_reason IS NOT NULL)");
        });

        builder.HasKey(task => task.Id);

        builder.Property(task => task.Id).HasColumnName("id");
        builder.Property(task => task.CreatedAt).HasColumnName("created_at");
        builder.Property(task => task.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(task => task.UpdatedAt).HasColumnName("updated_at");
        builder.Property(task => task.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(task => task.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(task => task.RoomId)
            .HasColumnName("room_id")
            .IsRequired();

        // Snapshot of the number the room carried when the task was planned: a renumbered room
        // must not rewrite the sheets of past days.
        builder.Property(task => task.RoomNumber)
            .HasColumnName("room_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(task => task.ServiceDate)
            .HasColumnName("service_date")
            .IsRequired();

        builder.Property(task => task.TaskType)
            .HasColumnName("task_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(task => task.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(task => task.AssignedTo).HasColumnName("assigned_to").HasMaxLength(160);
        builder.Property(task => task.AssignedAt).HasColumnName("assigned_at");
        builder.Property(task => task.AssignedBy).HasColumnName("assigned_by").HasMaxLength(160);
        builder.Property(task => task.StartedAt).HasColumnName("started_at");
        builder.Property(task => task.StartedBy).HasColumnName("started_by").HasMaxLength(160);
        builder.Property(task => task.CleanedAt).HasColumnName("cleaned_at");
        builder.Property(task => task.CleanedBy).HasColumnName("cleaned_by").HasMaxLength(160);
        builder.Property(task => task.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(task => task.InspectedAt).HasColumnName("inspected_at");
        builder.Property(task => task.InspectedBy).HasColumnName("inspected_by").HasMaxLength(160);
        builder.Property(task => task.InspectionNotes).HasColumnName("inspection_notes").HasMaxLength(300);
        builder.Property(task => task.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(task => task.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);
        builder.Property(task => task.CancelReason).HasColumnName("cancel_reason").HasMaxLength(300);
        builder.Property(task => task.Notes).HasColumnName("notes").HasMaxLength(300);

        builder.Ignore(task => task.IsClosed);

        // The idempotence guarantee of the day-sheet generation, held by the database rather
        // than by the service exists-check alone: one task of a given TYPE per room and per day.
        // Two supervisors generating the same sheet at the same time race past the check, collide
        // here, and the loser surfaces as a 409 - no room ends up on the sheet twice. Keying on
        // the type (and not on the room alone) is what still lets a deep clean be planned on a
        // room that already has its departure clean.
        builder.HasIndex(task => new { task.RoomId, task.ServiceDate, task.TaskType })
            .IsUnique()
            .HasDatabaseName("ux_housekeeping_tasks_room_date_type");

        // The two ways the module reads its tasks: the sheet of one unit for one day, and the
        // work of one attendant.
        builder.HasIndex(task => new { task.HotelUnitCode, task.ServiceDate })
            .HasDatabaseName("ix_housekeeping_tasks_unit_service_date");

        builder.HasIndex(task => task.AssignedTo)
            .HasDatabaseName("ix_housekeeping_tasks_assigned_to");

        builder.HasIndex(task => task.Status)
            .HasDatabaseName("ix_housekeeping_tasks_status");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(task => task.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(task => task.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
