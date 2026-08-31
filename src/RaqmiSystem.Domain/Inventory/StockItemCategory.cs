namespace RaqmiSystem.Domain.Inventory;

/// <summary>
/// Simple functional families for stock items. The list is deliberately short: it drives
/// filtering and reporting, not accounting - a finer taxonomy can be layered on later
/// without touching the movement registry.
/// </summary>
public enum StockItemCategory
{
    Alimentaire,
    Boisson,
    Entretien,
    Equipement,
    Autre
}
