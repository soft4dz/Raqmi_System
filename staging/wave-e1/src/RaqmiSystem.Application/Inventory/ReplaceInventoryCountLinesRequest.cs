namespace RaqmiSystem.Application.Inventory;

public sealed record ReplaceInventoryCountLinesRequest(
    IReadOnlyCollection<InventoryCountLineRequest> Lines);
