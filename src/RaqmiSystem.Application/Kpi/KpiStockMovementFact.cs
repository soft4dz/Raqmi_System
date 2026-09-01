using RaqmiSystem.Domain.Inventory;

namespace RaqmiSystem.Application.Kpi;

/// <summary>
/// Un mouvement de stock valorise, rattache a une unite par le MAGASIN qui l'a enregistre.
///
/// <paramref name="Quantity"/> est toujours positive, comme dans le registre d'origine ; le
/// sens vient de la nature du mouvement, transportee telle quelle. <paramref name="UnitCost"/>
/// peut manquer : un mouvement non valorise est signale comme donnee manquante par le
/// calculateur, jamais compte pour zero - un cout matiere qui ignore silencieusement les
/// sorties non valorisees serait faussement rassurant.
/// </summary>
public sealed record KpiStockMovementFact(
    string HotelUnitCode,
    string WarehouseCode,
    string ItemCode,
    DateOnly MovementDate,
    StockMovementKind Kind,
    decimal Quantity,
    decimal SignedQuantity,
    decimal? UnitCost);
