using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un article de stock, reduit a sa famille et a son activite. La famille est ce qui rattache
/// une consommation au food cost ou au beverage cost ; l'activite est ce qui evite de compter
/// un article retire du catalogue dans un taux de rupture.
/// </summary>
public sealed record KpiStockItemFact(string ItemCode, StockItemCategory Category, bool IsActive);
