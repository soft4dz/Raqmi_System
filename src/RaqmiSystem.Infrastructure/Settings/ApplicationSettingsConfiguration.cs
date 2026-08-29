using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Settings;

namespace RaqmiSystem.Infrastructure.Settings;

public sealed class ApplicationSettingsConfiguration : IEntityTypeConfiguration<ApplicationSettings>
{
    public void Configure(EntityTypeBuilder<ApplicationSettings> builder)
    {
        builder.ToTable("application_settings", "settings", table =>
        {
            // The singleton guarantee, half of it: the discriminating column can only ever hold
            // the single literal 'GLOBAL'. Combined with the unique index below, the table is
            // capped at exactly one row by the database itself.
            table.HasCheckConstraint(
                "ck_application_settings_singleton",
                "singleton_key = 'GLOBAL'");

            // Same rule as finance.customers: the emitter's NIF is held to the customer's format.
            table.HasCheckConstraint(
                "ck_application_settings_company_nif_length",
                "company_nif IS NULL OR length(company_nif) = 15");

            // The CAST is not cosmetic: the SQLite provider used by the test harness stores
            // decimal as TEXT, and '9.00' IN (0, 9, 19) is false there. Casting to numeric first
            // makes the very same constraint text mean the same thing on both providers.
            table.HasCheckConstraint(
                "ck_application_settings_default_vat_rate",
                "CAST(default_vat_rate AS numeric) IN (0, 9, 19)");

            table.HasCheckConstraint(
                "ck_application_settings_audit_retention_days",
                "audit_retention_days BETWEEN 30 AND 3650");
        });

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.Id).HasColumnName("id");
        builder.Property(settings => settings.CreatedAt).HasColumnName("created_at");
        builder.Property(settings => settings.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(settings => settings.UpdatedAt).HasColumnName("updated_at");
        builder.Property(settings => settings.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(settings => settings.SingletonKey)
            .HasColumnName("singleton_key")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(settings => settings.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(settings => settings.CompanyNif).HasColumnName("company_nif").HasMaxLength(15);
        builder.Property(settings => settings.CompanyRc).HasColumnName("company_rc").HasMaxLength(20);
        builder.Property(settings => settings.CompanyAi).HasColumnName("company_ai").HasMaxLength(20);
        builder.Property(settings => settings.CompanyNis).HasColumnName("company_nis").HasMaxLength(20);
        builder.Property(settings => settings.CompanyAddress).HasColumnName("company_address").HasMaxLength(200);
        builder.Property(settings => settings.CompanyCity).HasColumnName("company_city").HasMaxLength(80);
        builder.Property(settings => settings.CompanyPhone).HasColumnName("company_phone").HasMaxLength(40);
        builder.Property(settings => settings.CompanyEmail).HasColumnName("company_email").HasMaxLength(160);

        builder.Property(settings => settings.DefaultVatRate)
            .HasColumnName("default_vat_rate")
            .HasPrecision(5, 2);

        builder.Property(settings => settings.CurrencyLabel)
            .HasColumnName("currency_label")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(settings => settings.AuditRetentionDays)
            .HasColumnName("audit_retention_days");

        // The other half of the singleton guarantee: one row per key value, and the check
        // constraint above allows exactly one key value.
        builder.HasIndex(settings => settings.SingletonKey)
            .IsUnique()
            .HasDatabaseName("ux_application_settings_singleton_key");
    }
}
