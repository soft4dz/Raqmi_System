using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Reporting;

namespace RaqmiSystem.Infrastructure.Reporting;

/// <summary>
/// reporting.report_executions - the only table this module owns: the journal of report
/// executions. Results are never stored (a report is recomputed from the live modules on every
/// run), so the row stays small: code, normalized parameters as JSON text, row count, duration,
/// and the author/timestamp carried by the audit columns.
///
/// report_code is deliberately NOT a foreign key: the catalog lives in code
/// (<see cref="ReportCatalog"/>), not in a table, and a journal line must survive a report
/// leaving the catalog - the trace of what was pulled outlives the tool that pulled it.
/// </summary>
public sealed class ReportExecutionConfiguration : IEntityTypeConfiguration<ReportExecution>
{
    public void Configure(EntityTypeBuilder<ReportExecution> builder)
    {
        builder.ToTable("report_executions", "reporting", table =>
        {
            table.HasCheckConstraint(
                "ck_report_executions_row_count",
                "row_count >= 0");

            table.HasCheckConstraint(
                "ck_report_executions_duration",
                "duration_milliseconds >= 0");
        });

        builder.HasKey(execution => execution.Id);

        builder.Property(execution => execution.Id).HasColumnName("id");
        builder.Property(execution => execution.CreatedAt).HasColumnName("created_at");
        builder.Property(execution => execution.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(execution => execution.UpdatedAt).HasColumnName("updated_at");
        builder.Property(execution => execution.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(execution => execution.ReportCode)
            .HasColumnName("report_code")
            .HasMaxLength(ReportExecution.ReportCodeMaxLength)
            .IsRequired();

        builder.Property(execution => execution.ParametersJson)
            .HasColumnName("parameters_json")
            .HasMaxLength(ReportExecution.ParametersJsonMaxLength)
            .IsRequired();

        builder.Property(execution => execution.RowCount)
            .HasColumnName("row_count");

        builder.Property(execution => execution.DurationMilliseconds)
            .HasColumnName("duration_milliseconds");

        // The journal is read newest-first, usually filtered by report.
        builder.HasIndex(execution => execution.ReportCode)
            .HasDatabaseName("ix_report_executions_report_code");

        builder.HasIndex(execution => execution.CreatedAt)
            .HasDatabaseName("ix_report_executions_created_at");
    }
}
