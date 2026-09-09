using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RaqmiSystem.Domain.Billing;
using RaqmiSystem.Domain.Fiscalite;
using RaqmiSystem.Domain.Purchasing;

namespace RaqmiSystem.Infrastructure.Fiscalite;

internal static class FiscaliteAudit
{
    public static void Apply<T>(EntityTypeBuilder<T> b) where T : RaqmiSystem.Domain.Common.AuditableEntity
    {
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(160);
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(160);
    }
}

public sealed class VatSalesRegisterEntryConfiguration : IEntityTypeConfiguration<VatSalesRegisterEntry>
{
    public void Configure(EntityTypeBuilder<VatSalesRegisterEntry> b)
    {
        b.ToTable("vat_sales_register_entries", "fiscalite", t =>
        {
            t.HasCheckConstraint("ck_vat_sales_register_type", "type IN ('Vente','Avoir')");
            t.HasCheckConstraint("ck_vat_sales_register_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(vat_amount AS numeric) >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        b.Property(x => x.PieceNumber).HasColumnName("piece_number").HasMaxLength(60).IsRequired();
        b.Property(x => x.PieceDate).HasColumnName("piece_date");
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>();
        b.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.CustomerNif).HasColumnName("customer_nif").HasMaxLength(15);
        b.Property(x => x.BaseHt).HasColumnName("base_ht").HasPrecision(18, 2);
        b.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2);
        b.Ignore(x => x.Ttc);
        b.HasIndex(x => x.PieceDate).HasDatabaseName("ix_vat_sales_register_piece_date");
        b.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_vat_sales_register_invoice_id");
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VatPurchaseRegisterEntryConfiguration : IEntityTypeConfiguration<VatPurchaseRegisterEntry>
{
    public void Configure(EntityTypeBuilder<VatPurchaseRegisterEntry> b)
    {
        b.ToTable("vat_purchase_register_entries", "fiscalite", t =>
        {
            t.HasCheckConstraint("ck_vat_purchase_register_source", "source IN ('Manuel','Achats')");
            t.HasCheckConstraint("ck_vat_purchase_register_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(vat_amount AS numeric) >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.PurchaseOrderId).HasColumnName("purchase_order_id");
        b.Property(x => x.PieceNumber).HasColumnName("piece_number").HasMaxLength(60).IsRequired();
        b.Property(x => x.PieceDate).HasColumnName("piece_date");
        b.Property(x => x.SupplierName).HasColumnName("supplier_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.SupplierNif).HasColumnName("supplier_nif").HasMaxLength(15);
        b.Property(x => x.BaseHt).HasColumnName("base_ht").HasPrecision(18, 2);
        b.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2);
        b.Property(x => x.Source).HasColumnName("source").HasConversion<string>();
        b.Ignore(x => x.Ttc);
        b.HasIndex(x => x.PieceDate).HasDatabaseName("ix_vat_purchase_register_piece_date");
        b.HasIndex(x => x.PurchaseOrderId).HasDatabaseName("ix_vat_purchase_register_purchase_order_id");
        b.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VatDeclarationConfiguration : IEntityTypeConfiguration<VatDeclaration>
{
    public void Configure(EntityTypeBuilder<VatDeclaration> b)
    {
        b.ToTable("vat_declarations", "fiscalite", t =>
        {
            t.HasCheckConstraint("ck_vat_declaration_month", "month BETWEEN 1 AND 12");
            t.HasCheckConstraint("ck_vat_declaration_status", "status IN ('Calculee','Exportee','Declaree')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.Year).HasColumnName("year");
        b.Property(x => x.Month).HasColumnName("month");
        b.Property(x => x.BaseHtVentes).HasColumnName("base_ht_ventes").HasPrecision(18, 2);
        b.Property(x => x.TvaCollectee).HasColumnName("tva_collectee").HasPrecision(18, 2);
        b.Property(x => x.TvaDeductible).HasColumnName("tva_deductible").HasPrecision(18, 2);
        b.Property(x => x.CreditAnterieur).HasColumnName("credit_anterieur").HasPrecision(18, 2);
        b.Property(x => x.Solde).HasColumnName("solde").HasPrecision(18, 2);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        b.Property(x => x.DgiReference).HasColumnName("dgi_reference").HasMaxLength(100);
        b.HasIndex(x => new { x.Year, x.Month }).IsUnique().HasDatabaseName("ux_vat_declarations_period");
    }
}

public sealed class TeleDeclarationConfiguration : IEntityTypeConfiguration<TeleDeclaration>
{
    public void Configure(EntityTypeBuilder<TeleDeclaration> b)
    {
        b.ToTable("tele_declarations", "fiscalite", t =>
        {
            t.HasCheckConstraint("ck_tele_declaration_type", "type IN ('Tva')");
            t.HasCheckConstraint("ck_tele_declaration_status", "status IN ('Calculee','Exportee','Declaree')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.VatDeclarationId).HasColumnName("vat_declaration_id");
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>();
        b.Property(x => x.Year).HasColumnName("year");
        b.Property(x => x.Month).HasColumnName("month");
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        b.Property(x => x.DgiReference).HasColumnName("dgi_reference").HasMaxLength(100);
        b.Property(x => x.ExportedAt).HasColumnName("exported_at");
        b.HasIndex(x => x.VatDeclarationId).HasDatabaseName("ix_tele_declarations_vat_declaration_id");
        b.HasOne<VatDeclaration>().WithMany().HasForeignKey(x => x.VatDeclarationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WithholdingTaxEntryConfiguration : IEntityTypeConfiguration<WithholdingTaxEntry>
{
    public void Configure(EntityTypeBuilder<WithholdingTaxEntry> b)
    {
        b.ToTable("withholding_tax_entries", "fiscalite", t =>
            t.HasCheckConstraint("ck_withholding_tax_amounts", "CAST(base_ht AS numeric) >= 0 AND CAST(rate AS numeric) BETWEEN 0 AND 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.SupplierName).HasColumnName("supplier_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.BaseHt).HasColumnName("base_ht").HasPrecision(18, 2);
        b.Property(x => x.Rate).HasColumnName("rate").HasPrecision(5, 2);
        b.Property(x => x.MontantRetenu).HasColumnName("montant_retenu").HasPrecision(18, 2);
        b.Property(x => x.Date).HasColumnName("date");
        b.HasIndex(x => x.Date).HasDatabaseName("ix_withholding_tax_entries_date");
    }
}

public sealed class FiscalReturnConfiguration : IEntityTypeConfiguration<FiscalReturn>
{
    public void Configure(EntityTypeBuilder<FiscalReturn> b)
    {
        b.ToTable("fiscal_returns", "fiscalite", t => t.HasCheckConstraint("ck_fiscal_return_kind", "kind IN ('Simple','Avancee')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.Year).HasColumnName("year");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>();
        b.Property(x => x.GeneratedAt).HasColumnName("generated_at");
        b.HasIndex(x => new { x.Year, x.Kind }).IsUnique().HasDatabaseName("ux_fiscal_returns_year_kind");
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.FiscalReturnId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class FiscalReturnLineConfiguration : IEntityTypeConfiguration<FiscalReturnLine>
{
    public void Configure(EntityTypeBuilder<FiscalReturnLine> b)
    {
        b.ToTable("fiscal_return_lines", "fiscalite");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.FiscalReturnId).HasColumnName("fiscal_return_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        b.Property(x => x.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        b.HasIndex(x => x.FiscalReturnId).HasDatabaseName("ix_fiscal_return_lines_fiscal_return_id");
    }
}

public sealed class SifecTransmissionConfiguration : IEntityTypeConfiguration<SifecTransmission>
{
    public void Configure(EntityTypeBuilder<SifecTransmission> b)
    {
        b.ToTable("sifec_transmissions", "fiscalite", t =>
        {
            t.HasCheckConstraint("ck_sifec_transmission_status", "status IN ('Prepare','Soumis','Accepte','Rejete','Erreur')");
            t.HasCheckConstraint("ck_sifec_transmission_mode", "mode IN ('Sandbox','Production')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        FiscaliteAudit.Apply(b);
        b.Property(x => x.InvoiceId).HasColumnName("invoice_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
        b.Property(x => x.Uid).HasColumnName("uid").HasMaxLength(80);
        b.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(128).IsRequired();
        b.Property(x => x.QrPayload).HasColumnName("qr_payload").IsRequired();
        b.Property(x => x.Mode).HasColumnName("mode").HasConversion<string>();
        b.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        b.Property(x => x.ResponseMessage).HasColumnName("response_message").HasMaxLength(500);
        b.HasIndex(x => x.InvoiceId).HasDatabaseName("ix_sifec_transmissions_invoice_id");
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
