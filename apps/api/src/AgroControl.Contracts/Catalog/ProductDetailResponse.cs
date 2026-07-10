namespace AgroControl.Contracts.Catalog;

public sealed record ProductDetailResponse(
    Guid ProductId,
    string Name,
    string? Description,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool IsActive,
    bool AllowsFraction,
    string? SalesUnitLabel,
    ProductCategoryResponse? Category,
    BrandResponse? Brand,
    UnitOfMeasureResponse BaseUnit,
    ProductPriceResponse CurrentPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
