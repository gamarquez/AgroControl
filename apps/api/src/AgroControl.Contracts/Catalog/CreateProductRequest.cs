namespace AgroControl.Contracts.Catalog;

public sealed record CreateProductRequest(
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
