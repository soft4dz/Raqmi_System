using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns", "crm", table =>
        {
            table.HasCheckConstraint(
                "ck_campaigns_status",
                "status IN ('Draft', 'Scheduled', 'Running', 'Completed', 'Cancelled')");

            table.HasCheckConstraint(
                "ck_campaigns_channel",
                "channel IN ('Email', 'Sms', 'Phone', 'OnSite')");

            // A campaign runs over at least one day; the period is inclusive on both ends.
            table.HasCheckConstraint("ck_campaigns_dates", "end_date >= start_date");

            // The reason is what makes an abandoned campaign readable a year later; the domain
            // demands it, and the database refuses the pair the domain could never produce.
            table.HasCheckConstraint(
                "ck_campaigns_cancel_reason",
                "(status <> 'Cancelled') OR (cancel_reason IS NOT NULL)");
        });

        builder.HasKey(campaign => campaign.Id);

        builder.Property(campaign => campaign.Id).HasColumnName("id");
        builder.Property(campaign => campaign.CreatedAt).HasColumnName("created_at");
        builder.Property(campaign => campaign.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(campaign => campaign.UpdatedAt).HasColumnName("updated_at");
        builder.Property(campaign => campaign.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(campaign => campaign.Code)
            .HasColumnName("code")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(campaign => campaign.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(campaign => campaign.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(campaign => campaign.TargetSegmentCode)
            .HasColumnName("target_segment_code")
            .HasMaxLength(40);

        builder.Property(campaign => campaign.StartDate).HasColumnName("start_date");
        builder.Property(campaign => campaign.EndDate).HasColumnName("end_date");

        builder.Property(campaign => campaign.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(campaign => campaign.Objective)
            .HasColumnName("objective")
            .HasMaxLength(400);

        builder.Property(campaign => campaign.Message)
            .HasColumnName("message")
            .HasMaxLength(2000);

        builder.Property(campaign => campaign.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(campaign => campaign.ScheduledBy).HasColumnName("scheduled_by").HasMaxLength(160);
        builder.Property(campaign => campaign.LaunchedAt).HasColumnName("launched_at");
        builder.Property(campaign => campaign.LaunchedBy).HasColumnName("launched_by").HasMaxLength(160);
        builder.Property(campaign => campaign.CompletedAt).HasColumnName("completed_at");
        builder.Property(campaign => campaign.CompletedBy).HasColumnName("completed_by").HasMaxLength(160);
        builder.Property(campaign => campaign.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(campaign => campaign.CancelledBy).HasColumnName("cancelled_by").HasMaxLength(160);

        builder.Property(campaign => campaign.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(500);

        builder.Ignore(campaign => campaign.CanEdit);
        builder.Ignore(campaign => campaign.RequiresMarketingConsent);

        builder.HasIndex(campaign => campaign.Code)
            .IsUnique()
            .HasDatabaseName("ux_campaigns_code");

        builder.HasIndex(campaign => campaign.Status)
            .HasDatabaseName("ix_campaigns_status");

        builder.HasIndex(campaign => campaign.TargetSegmentCode)
            .HasDatabaseName("ix_campaigns_target_segment_code");

        builder.HasOne<CustomerSegment>()
            .WithMany()
            .HasPrincipalKey(segment => segment.Code)
            .HasForeignKey(campaign => campaign.TargetSegmentCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
