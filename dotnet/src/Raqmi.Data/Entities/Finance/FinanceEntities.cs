namespace Raqmi.Data.Entities.Finance;

public class DailyRevenueEntry : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = "general";
    public string Status { get; set; } = "draft";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Invoice : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InvoiceLine> Lines { get; set; } = [];
}

public class InvoiceLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string InvoiceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }

    public Invoice Invoice { get; set; } = null!;
}

public class TreasuryMovement : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string Type { get; set; } = "in";
    public string Account { get; set; } = "cash";
    public decimal Amount { get; set; }
    public DateOnly MovementDate { get; set; }
    public string? Reference { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
