using AgroControl.Contracts.Catalog;

namespace AgroControl.Contracts.Stock;

public sealed record StockDetailResponse(
    WarehouseResponse Warehouse,
    Guid ProductId,
    string Name,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool IsActive,
    bool AllowsFraction,
    ProductCategoryResponse? Category,
    BrandResponse? Brand,
    UnitOfMeasureResponse BaseUnit,
    decimal OnHandQuantity,
    bool IsLowStock,
    DateTimeOffset? LastMovementAt,
    StockPolicyResponse Policy);
