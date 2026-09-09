namespace RaqmiSystem.Application.Purchasing;

/// <summary>
/// One line of a purchase order at capture time. The designation is frozen into the line (it
/// describes what was ordered, worded as it was ordered); the item code must reference an
/// existing stock item - existence is checked by the service through the stock module's
/// published contract.
/// </summary>
/// <param name="VatRate">
/// Algerian VAT rate (0, 9 or 19) applied to the line. Null defaults to
/// <see cref="RaqmiSystem.Domain.Purchasing.PurchaseOrderLine.DefaultVatRate"/> (19%), the same
/// default Facturation uses - the VAT purchases register (module Fiscalite) reads whatever is
/// stored here, so a wrong rate here is a wrong register line, not just a wrong total.
/// </param>
public sealed record PurchaseOrderLineRequest(
    string ItemCode,
    string Designation,
    decimal Quantity,
    decimal UnitPrice,
    decimal? VatRate = null);
