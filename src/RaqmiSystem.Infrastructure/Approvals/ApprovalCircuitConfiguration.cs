using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Approvals;

namespace RaqmiSystem.Infrastructure.Approvals;

public sealed class ApprovalCircuitConfiguration : IEntityTypeConfiguration<ApprovalCircuit>
{
    public void Configure(EntityTypeBuilder<ApprovalCircuit> builder)
    {
        builder.ToTable("approval_circuits", "approvals", table =>
        {
            table.HasCheckConstraint(
                "ck_approval_circuits_subject_type",
                "subject_type IN ('PaymentOrder')");
        });

        builder.HasKey(circuit => circuit.Id);

        builder.Property(circuit => circuit.Id).HasColumnName("id");
        builder.Property(circuit => circuit.CreatedAt).HasColumnName("created_at");
        builder.Property(circuit => circuit.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(circuit => circuit.UpdatedAt).HasColumnName("updated_at");
        builder.Property(circuit => circuit.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(circuit => circuit.Code)
            .HasColumnName("code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(circuit => circuit.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(circuit => circuit.SubjectType)
            .HasColumnName("subject_type")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(circuit => circuit.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(circuit => circuit.Code)
            .IsUnique()
            .HasDatabaseName("ux_approval_circuits_code");

        // The gate and the instance-opening path both ask "which ACTIVE circuit covers this
        // subject type?": this index answers without scanning inactive circuits.
        builder.HasIndex(circuit => circuit.SubjectType)
            .HasDatabaseName("ix_approval_circuits_subject_type");

        builder.HasMany(circuit => circuit.Steps)
            .WithOne()
            .HasForeignKey(step => step.CircuitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Steps is an IReadOnlyCollection backed by the _steps field; EF must mutate the field,
        // never the read-only projection.
        builder.Navigation(circuit => circuit.Steps)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
