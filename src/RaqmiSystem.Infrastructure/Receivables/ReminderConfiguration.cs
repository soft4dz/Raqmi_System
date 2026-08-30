using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Receivables;

namespace RaqmiSystem.Infrastructure.Receivables;

/// <summary>
/// finance.reminders - the only table this module owns. It lives in the existing "finance"
/// schema alongside invoices and treasury.
///
/// Why <c>invoice_number</c> is NOT a foreign key: EF Core can only target a principal key
/// built on required (non-nullable) properties, and finance.invoices.number is nullable by
/// design - a draft invoice has no legal number yet, one is only allocated at issue time.
/// Declaring the alternate key would force the column to be required and break the whole
/// draft-then-issue cycle of the billing module, which this module must not disturb. The
/// referential rule is therefore enforced one level up, in ReceivablesService: the invoice is
/// loaded and must exist and be Issued before a reminder is accepted. The customer link, whose
/// principal key (customers.code) IS required, remains a real foreign key.
/// </summary>
public sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders", "finance", table =>
        {
            table.HasCheckConstraint(
                "ck_reminders_level",
                "level IN ('First', 'Second', 'FormalNotice')");

            table.HasCheckConstraint(
                "ck_reminders_channel",
                "channel IN ('Phone', 'Email', 'Letter', 'InPerson')");
        });

        builder.HasKey(reminder => reminder.Id);

        builder.Property(reminder => reminder.Id).HasColumnName("id");
        builder.Property(reminder => reminder.CreatedAt).HasColumnName("created_at");
        builder.Property(reminder => reminder.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(reminder => reminder.UpdatedAt).HasColumnName("updated_at");
        builder.Property(reminder => reminder.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(reminder => reminder.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(reminder => reminder.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(Reminder.InvoiceNumberMaxLength)
            .IsRequired();

        builder.Property(reminder => reminder.Level)
            .HasColumnName("level")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(reminder => reminder.SentAt)
            .HasColumnName("sent_at");

        builder.Property(reminder => reminder.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(reminder => reminder.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        // The escalation ladder is climbed once per invoice: the same level cannot be recorded
        // twice for the same invoice. ReceivablesService checks it first for a clean 409, and
        // this index is what actually makes the rule true under concurrency.
        builder.HasIndex(reminder => new { reminder.InvoiceNumber, reminder.Level })
            .IsUnique()
            .HasDatabaseName("ux_reminders_invoice_number_level");

        builder.HasIndex(reminder => reminder.CustomerCode)
            .HasDatabaseName("ix_reminders_customer_code");

        builder.HasIndex(reminder => reminder.SentAt)
            .HasDatabaseName("ix_reminders_sent_at");

        builder.HasOne<Customer>()
            .WithMany()
            .HasPrincipalKey(customer => customer.Code)
            .HasForeignKey(reminder => reminder.CustomerCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
