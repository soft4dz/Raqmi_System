using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Accounting;

namespace RaqmiSystem.Infrastructure.Accounting;

public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> b)
    {
        b.ToTable("fiscal_years", "accounting", t => { t.HasCheckConstraint("ck_fiscal_year_dates", "ends_on >= starts_on"); t.HasCheckConstraint("ck_fiscal_year_status", "status IN ('Open','Closed')"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        b.Property(x => x.StartsOn).HasColumnName("starts_on"); b.Property(x => x.EndsOn).HasColumnName("ends_on"); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        Audit(b); b.Property(x => x.ClosedAt).HasColumnName("closed_at"); b.Property(x => x.ClosedBy).HasColumnName("closed_by").HasMaxLength(160);
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_fiscal_years_code");
    }
    internal static void Audit<T>(EntityTypeBuilder<T> b) where T : RaqmiSystem.Domain.Common.AuditableEntity { b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(160); b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160); }
}
public sealed class AccountingPeriodConfiguration : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> b) { b.ToTable("periods", "accounting", t => { t.HasCheckConstraint("ck_period_dates", "ends_on >= starts_on"); t.HasCheckConstraint("ck_period_number", "number BETWEEN 1 AND 16"); }); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.FiscalYearId).HasColumnName("fiscal_year_id"); b.Property(x=>x.Number).HasColumnName("number"); b.Property(x=>x.StartsOn).HasColumnName("starts_on"); b.Property(x=>x.EndsOn).HasColumnName("ends_on"); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>(); b.Property(x=>x.ClosedAt).HasColumnName("closed_at"); b.Property(x=>x.ClosedBy).HasColumnName("closed_by").HasMaxLength(160); FiscalYearConfiguration.Audit(b); b.HasIndex(x=>new{x.FiscalYearId,x.Number}).IsUnique(); b.HasOne<FiscalYear>().WithMany().HasForeignKey(x=>x.FiscalYearId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class AccountingPartyConfiguration : IEntityTypeConfiguration<AccountingParty>
{
    public void Configure(EntityTypeBuilder<AccountingParty> b) { b.ToTable("parties","accounting"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.Code).HasColumnName("code").HasMaxLength(40); b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x=>x.Kind).HasColumnName("kind").HasConversion<string>(); b.Property(x=>x.IsActive).HasColumnName("is_active"); FiscalYearConfiguration.Audit(b); b.HasIndex(x=>x.Code).IsUnique(); }
}
public sealed class JournalSequenceConfiguration : IEntityTypeConfiguration<JournalSequence>
{
    public void Configure(EntityTypeBuilder<JournalSequence> b) { b.ToTable("journal_sequences","accounting",t=>t.HasCheckConstraint("ck_journal_sequence_non_negative","last_number >= 0")); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.JournalCode).HasColumnName("journal_code").HasMaxLength(10); b.Property(x=>x.FiscalYearId).HasColumnName("fiscal_year_id"); b.Property(x=>x.LastNumber).HasColumnName("last_number").IsConcurrencyToken(); b.HasIndex(x=>new{x.JournalCode,x.FiscalYearId}).IsUnique(); b.HasOne<AccountingJournal>().WithMany().HasPrincipalKey(x=>x.Code).HasForeignKey(x=>x.JournalCode).OnDelete(DeleteBehavior.Restrict); b.HasOne<FiscalYear>().WithMany().HasForeignKey(x=>x.FiscalYearId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class ReconciliationConfiguration : IEntityTypeConfiguration<Reconciliation>
{
    public void Configure(EntityTypeBuilder<Reconciliation> b) { b.ToTable("reconciliations","accounting",t=>t.HasCheckConstraint("ck_reconciliation_amount","matched_amount > 0")); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.Code).HasColumnName("code").HasMaxLength(40); b.Property(x=>x.PartyId).HasColumnName("party_id"); b.Property(x=>x.MatchedAmount).HasColumnName("matched_amount").HasPrecision(18,2); b.Property(x=>x.Status).HasColumnName("status").HasConversion<string>(); FiscalYearConfiguration.Audit(b); b.HasIndex(x=>x.Code).IsUnique(); b.HasOne<AccountingParty>().WithMany().HasForeignKey(x=>x.PartyId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x=>x.Allocations).WithOne().HasForeignKey(x=>x.ReconciliationId).OnDelete(DeleteBehavior.Cascade); b.Navigation(x=>x.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field); }
}
public sealed class ReconciliationAllocationConfiguration : IEntityTypeConfiguration<ReconciliationAllocation>
{
    public void Configure(EntityTypeBuilder<ReconciliationAllocation> b) { b.ToTable("reconciliation_allocations","accounting",t=>t.HasCheckConstraint("ck_reconciliation_allocation_amount","amount > 0")); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasColumnName("id").ValueGeneratedNever(); b.Property(x=>x.ReconciliationId).HasColumnName("reconciliation_id"); b.Property(x=>x.JournalEntryLineId).HasColumnName("journal_entry_line_id"); b.Property(x=>x.Side).HasColumnName("side").HasConversion<string>(); b.Property(x=>x.Amount).HasColumnName("amount").HasPrecision(18,2); b.HasIndex(x=>x.JournalEntryLineId); b.HasOne<JournalEntryLine>().WithMany().HasForeignKey(x=>x.JournalEntryLineId).OnDelete(DeleteBehavior.Restrict); }
}
