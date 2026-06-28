using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Raqmi.Data.Migrations;

public partial class TenantSettingsExtended : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "TradeName", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "LegalForm", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "RegistrationNumber", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TaxId", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "StatisticalId", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "VatArticle", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ActivitySector", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Website", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "City", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Wilaya", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PostalCode", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Country", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "InvoicePrefix", table: "tenant_settings", type: "text", nullable: false, defaultValue: "FAC");
        migrationBuilder.AddColumn<string>(name: "QuotePrefix", table: "tenant_settings", type: "text", nullable: false, defaultValue: "DEV");
        migrationBuilder.AddColumn<int>(name: "NextInvoiceNumber", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(name: "FiscalYearStartMonth", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<decimal>(name: "DefaultVatRate", table: "tenant_settings", type: "numeric", nullable: false, defaultValue: 19m);
        migrationBuilder.AddColumn<string>(name: "InvoiceFooter", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "AcceptedPaymentMethods", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "BrandSecondaryColor", table: "tenant_settings", type: "text", nullable: true);
        migrationBuilder.AddColumn<int>(name: "SessionTimeoutMinutes", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 480);
        migrationBuilder.AddColumn<int>(name: "PasswordMinLength", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 8);
        migrationBuilder.AddColumn<int>(name: "ForcePasswordChangeDays", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 90);
        migrationBuilder.AddColumn<string>(name: "StorageDriver", table: "tenant_settings", type: "text", nullable: false, defaultValue: "local");
        migrationBuilder.AddColumn<int>(name: "MaxUploadSizeMb", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 25);
        migrationBuilder.AddColumn<string>(name: "BackupFrequency", table: "tenant_settings", type: "text", nullable: false, defaultValue: "daily");
        migrationBuilder.AddColumn<int>(name: "BackupRetentionDays", table: "tenant_settings", type: "integer", nullable: false, defaultValue: 30);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TradeName", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "LegalForm", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "RegistrationNumber", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "TaxId", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "StatisticalId", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "VatArticle", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "ActivitySector", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "Website", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "City", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "Wilaya", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "PostalCode", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "Country", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "InvoicePrefix", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "QuotePrefix", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "NextInvoiceNumber", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "FiscalYearStartMonth", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "DefaultVatRate", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "InvoiceFooter", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "AcceptedPaymentMethods", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "BrandSecondaryColor", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "SessionTimeoutMinutes", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "PasswordMinLength", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "ForcePasswordChangeDays", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "StorageDriver", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "MaxUploadSizeMb", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "BackupFrequency", table: "tenant_settings");
        migrationBuilder.DropColumn(name: "BackupRetentionDays", table: "tenant_settings");
    }
}
