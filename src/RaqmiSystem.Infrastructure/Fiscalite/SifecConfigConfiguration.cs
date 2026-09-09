using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Fiscalite;

namespace RaqmiSystem.Infrastructure.Fiscalite;

public sealed class SifecConfigConfiguration : IEntityTypeConfiguration<SifecConfig>
{
    public void Configure(EntityTypeBuilder<SifecConfig> builder)
    {
        builder.ToTable("sifec_config", "fiscalite", table =>
        {
            table.HasCheckConstraint("ck_sifec_config_singleton", "singleton_key = 'GLOBAL'");
            table.HasCheckConstraint("ck_sifec_config_mode", "mode IN ('Sandbox','Production')");
            table.HasCheckConstraint("ck_sifec_config_declarant_nif_length", "declarant_nif IS NULL OR length(declarant_nif) = 15");
        });

        builder.HasKey(config => config.Id);

        builder.Property(config => config.Id).HasColumnName("id");
        FiscaliteAudit.Apply(builder);

        builder.Property(config => config.SingletonKey).HasColumnName("singleton_key").HasMaxLength(10).IsRequired();
        builder.Property(config => config.Mode).HasColumnName("mode").HasConversion<string>();
        builder.Property(config => config.ApiUrl).HasColumnName("api_url").HasMaxLength(300);
        builder.Property(config => config.ApiKeyReference).HasColumnName("api_key_reference").HasMaxLength(200);
        builder.Property(config => config.DeclarantNif).HasColumnName("declarant_nif").HasMaxLength(15);
        builder.Property(config => config.IsActive).HasColumnName("is_active");
        builder.Property(config => config.LastConnectionTestAt).HasColumnName("last_connection_test_at");
        builder.Property(config => config.LastConnectionTestSucceeded).HasColumnName("last_connection_test_succeeded");

        builder.HasIndex(config => config.SingletonKey)
            .IsUnique()
            .HasDatabaseName("ux_sifec_config_singleton_key");
    }
}
