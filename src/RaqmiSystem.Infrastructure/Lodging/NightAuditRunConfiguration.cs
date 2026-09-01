using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Lodging;
using RaqmiSystem.Domain.Organization;

namespace RaqmiSystem.Infrastructure.Lodging;

public sealed class NightAuditRunConfiguration : IEntityTypeConfiguration<NightAuditRun>
{
    public void Configure(EntityTypeBuilder<NightAuditRun> builder)
    {
        builder.ToTable("night_audit_runs", "lodging", table =>
        {
            table.HasCheckConstraint(
                "ck_night_audit_runs_status",
                "status IN ('Inspected', 'Completed', 'Blocked')");
        });

        builder.HasKey(run => run.Id);

        builder.Property(run => run.Id).HasColumnName("id");
        builder.Property(run => run.CreatedAt).HasColumnName("created_at");
        builder.Property(run => run.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(run => run.UpdatedAt).HasColumnName("updated_at");
        builder.Property(run => run.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(run => run.HotelUnitCode)
            .HasColumnName("hotel_unit_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(run => run.BusinessDate).HasColumnName("business_date");

        builder.Property(run => run.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(run => run.StartedAt).HasColumnName("started_at");
        builder.Property(run => run.StartedBy).HasColumnName("started_by").HasMaxLength(160);
        builder.Property(run => run.CompletedAt).HasColumnName("completed_at");
        builder.Property(run => run.CompletedBy).HasColumnName("completed_by").HasMaxLength(160);

        builder.Property(run => run.PostedRoomNights).HasColumnName("posted_room_nights");
        builder.Property(run => run.PostedExtras).HasColumnName("posted_extras");
        builder.Property(run => run.PostedAmount).HasColumnName("posted_amount").HasPrecision(18, 2);
        builder.Property(run => run.NoShowsRecorded).HasColumnName("no_shows_recorded");
        builder.Property(run => run.SkippedAlreadyPosted).HasColumnName("skipped_already_posted");

        builder.Property(run => run.PendingArrivals).HasColumnName("pending_arrivals");
        builder.Property(run => run.PendingDepartures).HasColumnName("pending_departures");
        builder.Property(run => run.OpenFolios).HasColumnName("open_folios");
        builder.Property(run => run.RoomStateMismatches).HasColumnName("room_state_mismatches");

        builder.Property(run => run.Report)
            .HasColumnName("report")
            .HasMaxLength(NightAuditRun.ReportMaxLength);

        // Un seul passage EXECUTE par journee et par unite. L'index est filtre sur le statut :
        // une repetition (Inspected) ou un passage refuse (Blocked) peuvent se rejouer autant de
        // fois qu'on veut - ils n'ecrivent rien - alors qu'un passage execute est unique par
        // construction. C'est le premier des deux verrous d'idempotence ; le second, celui qui
        // compte vraiment, est la reference de geste unique par folio.
        builder.HasIndex(
                run => new { run.HotelUnitCode, run.BusinessDate },
                "ux_night_audit_runs_unit_business_date_completed")
            .IsUnique()
            .HasFilter("status = 'Completed'")
            .HasDatabaseName("ux_night_audit_runs_unit_business_date_completed");

        builder.HasIndex(
                run => new { run.HotelUnitCode, run.BusinessDate },
                "ix_night_audit_runs_unit_business_date")
            .HasDatabaseName("ix_night_audit_runs_unit_business_date");

        builder.HasOne<HotelUnit>()
            .WithMany()
            .HasPrincipalKey(unit => unit.Code)
            .HasForeignKey(run => run.HotelUnitCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
