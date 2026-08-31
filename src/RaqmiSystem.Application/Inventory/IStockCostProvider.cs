using RaqmiSystem.Application.Common;

namespace RaqmiSystem.Application.Inventory;

public interface IStockCostProvider
{
    // Cout moyen pondere courant d'un article (toutes entrees confondues).
    Task<ApplicationResult<ItemCost>> GetAverageCostAsync(string itemCode, CancellationToken cancellationToken);
}

public sealed record ItemCost(string ItemCode, decimal AverageUnitCost, string UnitOfMeasure);
