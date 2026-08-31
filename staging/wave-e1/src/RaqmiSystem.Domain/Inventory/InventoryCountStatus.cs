namespace RaqmiSystem.Domain.Inventory;

public enum InventoryCountStatus
{
    /// <summary>Counting in progress: lines can still be replaced.</summary>
    Draft,

    /// <summary>
    /// Validated: the adjustment movements have been generated and the count is immutable -
    /// it is now the documentary proof behind those adjustments.
    /// </summary>
    Validated
}
