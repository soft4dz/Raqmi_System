namespace Raqmi.Data.Entities.Stocks;

public class Product : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "u";
    public decimal MinStockLevel { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StockMovement : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string Type { get; set; } = "in";
    public decimal Quantity { get; set; }
    public DateOnly MovementDate { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}

public class InventorySession : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public DateOnly SessionDate { get; set; }
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryLine> Lines { get; set; } = [];
}

public class InventoryLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public decimal ExpectedQty { get; set; }
    public decimal CountedQty { get; set; }

    public InventorySession Session { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
