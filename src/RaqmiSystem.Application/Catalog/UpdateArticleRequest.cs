namespace RaqmiSystem.Application.Catalog;

public sealed record UpdateArticleRequest(
    string Designation,
    string UnitOfMeasure,
    decimal VatRate,
    decimal UnitPriceExclVat,
    string? Family = null,
    bool TracksStock = false,
    string? StockItemCode = null);
