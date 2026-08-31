using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Crm;

namespace RaqmiSystem.Infrastructure.Crm;

public sealed class CustomerSegmentConfiguration : IEntityTypeConfiguration<CustomerSegment>
{
    public void Configure(EntityTypeBuilder<CustomerSegment> builder)
    {
        builder.ToTable("customer_segments", "crm");

        builder.HasKey(segment => segment.Id);

        builder.Property(segment => segment.Id).HasColumnName("id");
        builder.Property(segment => segment.CreatedAt).HasColumnName("created_at");
        builder.Property(segment => segment.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(segment => segment.UpdatedAt).HasColumnName("updated_at");
        builder.Property(segment => segment.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(segment => segment.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(segment => segment.Label)
            .HasColumnName("label")
            .HasMaxLength(160)
            .IsRequired();

        // Bound aligned on CrmText.Optional in the entity: without it the column would be created
        // unbounded and the database would accept what the entity refuses.
        builder.Property(segment => segment.Description)
            .HasColumnName("description")
            .HasMaxLength(400);

        builder.Property(segment => segment.IsActive)
            .HasColumnName("is_active");

        // No separate unique index on Code: GuestProfileConfiguration's and
        // CampaignConfiguration's HasPrincipalKey(segment => segment.Code) already force EF to
        // create an alternate-key unique constraint on this column, exactly like the customer
        // file relies on the invoices' alternate key.
    }
}
