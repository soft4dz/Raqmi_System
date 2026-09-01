using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Kpi;

namespace RaqmiSystem.Infrastructure.Kpi;

/// <summary>
/// Le rattachement des comptes du plan comptable aux groupes de gestion qui construisent le
/// GOP, l'EBE et les marges.
///
/// AUCUNE DONNEE N'EST SEMEE dans cette table, et c'est deliberé : reproduire de memoire la
/// nomenclature SCF presenterait des codes inventes comme une reference reglementaire - la meme
/// raison qui fait que le module Comptabilite ne livre pas de plan comptable. Le mapping est
/// saisi et verifie par le comptable de l'etablissement.
/// </summary>
public sealed class KpiAccountMappingConfiguration : IEntityTypeConfiguration<KpiAccountMapping>
{
    public void Configure(EntityTypeBuilder<KpiAccountMapping> builder)
    {
        builder.ToTable("kpi_account_mappings", "kpi", table =>
        {
            table.HasCheckConstraint(
                "ck_kpi_account_mappings_group",
                "\"group\" IN ('Revenue', 'DepartmentalExpense', 'UndistributedExpense', "
                + "'FixedCharge', 'DepreciationAndProvision', 'FinancialResult', 'IncomeTax')");
        });

        builder.HasKey(mapping => mapping.Id);

        builder.Property(mapping => mapping.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(mapping => mapping.CreatedAt).HasColumnName("created_at");
        builder.Property(mapping => mapping.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        builder.Property(mapping => mapping.UpdatedAt).HasColumnName("updated_at");
        builder.Property(mapping => mapping.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);

        builder.Property(mapping => mapping.AccountPrefix)
            .HasColumnName("account_prefix")
            .HasMaxLength(KpiAccountMapping.MaxPrefixLength)
            .IsRequired();

        builder.Property(mapping => mapping.Group)
            .HasColumnName("group")
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(mapping => mapping.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mapping => mapping.IsActive).HasColumnName("is_active");

        // Un prefixe ne peut etre rattache qu'a UN groupe : "60" ne peut pas etre a la fois une
        // charge departementale et une charge non repartie, sinon le meme compte compterait deux
        // fois dans le GOP. Les exceptions s'ecrivent avec un prefixe PLUS LONG ("603"), que le
        // calculateur fait gagner.
        builder.HasIndex(mapping => mapping.AccountPrefix)
            .IsUnique()
            .HasDatabaseName("ux_kpi_account_mappings_prefix");
    }
}
