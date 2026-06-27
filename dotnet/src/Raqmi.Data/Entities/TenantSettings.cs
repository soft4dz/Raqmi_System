namespace Raqmi.Data.Entities;

public class TenantSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Currency { get; set; } = "DZD";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string NumberFormat { get; set; } = "fr-DZ";
    public string Timezone { get; set; } = "Africa/Algiers";
    public int PaymentDelayDays { get; set; } = 30;
    public int ReminderDelayDays { get; set; } = 7;
    public string? BrandPrimaryColor { get; set; } = "#2563eb";
    public string? BrandLogoUrl { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
}
