namespace RaqmiSystem.Application.Catalog;

public sealed record CreateArticleRequest(
    string Code,
    string Designation,
    string UnitOfMeasure,
    decimal VatRate,
    decimal UnitPriceExclVat,
    string? Family = null,
    bool TracksStock = false,
    string? StockItemCode = null);
