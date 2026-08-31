namespace RaqmiSystem.Application.Purchasing;

/// <summary>
/// One line of a purchase order at capture time. The designation is frozen into the line (it
/// describes what was ordered, worded as it was ordered); the item code must reference an
/// existing stock item - existence is checked by the service through the stock module's
/// published contract.
/// </summary>
public sealed record PurchaseOrderLineRequest(
    string ItemCode,
    string Designation,
    decimal Quantity,
    decimal UnitPrice);
