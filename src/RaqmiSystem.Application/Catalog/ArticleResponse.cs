namespace RaqmiSystem.Application.Catalog;

public sealed record ArticleResponse(
    Guid Id,
    string Code,
    string Designation,
    string? Family,
    string UnitOfMeasure,
    decimal VatRate,
    decimal UnitPriceExclVat,
    bool TracksStock,
    string? StockItemCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);
