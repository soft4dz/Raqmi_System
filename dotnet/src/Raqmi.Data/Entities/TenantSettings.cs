namespace Raqmi.Data.Entities;

public class TenantSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TradeName { get; set; }
    public string? LegalForm { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TaxId { get; set; }
    public string? StatisticalId { get; set; }
    public string? VatArticle { get; set; }
    public string? ActivitySector { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Wilaya { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "Algérie";
    public string Currency { get; set; } = "DZD";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string NumberFormat { get; set; } = "fr-DZ";
    public string Timezone { get; set; } = "Africa/Algiers";
    public int PaymentDelayDays { get; set; } = 30;
    public int ReminderDelayDays { get; set; } = 7;
    public string InvoicePrefix { get; set; } = "FAC";
    public string QuotePrefix { get; set; } = "DEV";
    public int NextInvoiceNumber { get; set; } = 1;
    public int FiscalYearStartMonth { get; set; } = 1;
    public decimal DefaultVatRate { get; set; } = 19;
    public string? InvoiceFooter { get; set; }
    public string? AcceptedPaymentMethods { get; set; }
    public string? BrandPrimaryColor { get; set; } = "#2563eb";
    public string? BrandSecondaryColor { get; set; } = "#0f766e";
    public string? BrandLogoUrl { get; set; }
    public int SessionTimeoutMinutes { get; set; } = 480;
    public int PasswordMinLength { get; set; } = 8;
    public int ForcePasswordChangeDays { get; set; } = 90;
    public string StorageDriver { get; set; } = "local";
    public int MaxUploadSizeMb { get; set; } = 25;
    public string BackupFrequency { get; set; } = "daily";
    public int BackupRetentionDays { get; set; } = 30;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
