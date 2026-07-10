namespace AgroControl.Application.Catalog;

public sealed record ProductCategoryRecord(
    Guid CategoryId,
    Guid OrganizationId,
    string Name,
    string? Description,
    bool IsActive);

public sealed record BrandRecord(
    Guid BrandId,
    Guid OrganizationId,
    string Name,
    string? Description,
    bool IsActive);

public sealed record UnitOfMeasureRecord(
    Guid UnitId,
    Guid OrganizationId,
    string Name,
    string Code,
    string Symbol,
    bool AllowsFraction,
    bool IsActive);

public sealed record PriceListRecord(
    Guid PriceListId,
    Guid OrganizationId,
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);

public sealed record ProductPriceRecord(
    Guid PriceListId,
    string PriceListName,
    string PriceListCode,
    decimal CostAmount,
    decimal? MarginPercent,
    decimal SaleAmount,
    string CurrencyCode,
    DateTimeOffset EffectiveFrom);

public sealed record ProductRecord(
    Guid ProductId,
    Guid OrganizationId,
    string Name,
    string? Description,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool IsActive,
    bool AllowsFraction,
    string? SalesUnitLabel,
    ProductCategoryRecord? Category,
    BrandRecord? Brand,
    UnitOfMeasureRecord BaseUnit,
    ProductPriceRecord CurrentPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProductListQuery(
    string? Search,
    Guid? CategoryId,
    Guid? BrandId,
    bool? IsActive,
    int Page,
    int PageSize);

public sealed record ProductListResult(
    IReadOnlyList<ProductRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CreateProductCommand(
    Guid? CategoryId,
    Guid? BrandId,
    Guid BaseUnitId,
    string Name,
    string? Description,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool AllowsFraction,
    string? SalesUnitLabel,
    decimal CostAmount,
    decimal? MarginPercent,
    decimal SaleAmount,
    string CurrencyCode,
    Guid? PriceListId);

public sealed record UpdateProductCommand(
    Guid? CategoryId,
    Guid? BrandId,
    Guid BaseUnitId,
    string Name,
    string? Description,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool IsActive,
    bool AllowsFraction,
    string? SalesUnitLabel,
    decimal CostAmount,
    decimal? MarginPercent,
    decimal SaleAmount,
    string CurrencyCode,
    Guid? PriceListId);

public sealed record CreateCategoryCommand(
    string Name,
    string? Description);

public sealed record UpdateCategoryCommand(
    string Name,
    string? Description,
    bool IsActive);

public sealed record CreateBrandCommand(
    string Name,
    string? Description);

public sealed record UpdateBrandCommand(
    string Name,
    string? Description,
    bool IsActive);

public sealed record CreatePriceListCommand(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);

public sealed record UpdatePriceListCommand(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
