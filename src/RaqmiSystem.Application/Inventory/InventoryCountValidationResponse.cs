namespace RaqmiSystem.Application.Inventory;

/// <summary>
/// Outcome of validating an inventory count: the now-immutable count, and how many
/// adjustment movements the validation generated (zero when every counted quantity
/// matched the theoretical stock).
/// </summary>
public sealed record InventoryCountValidationResponse(
    InventoryCountResponse Count,
    int AdjustmentCount);
