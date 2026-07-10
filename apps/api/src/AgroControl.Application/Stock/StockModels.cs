using AgroControl.Application.Catalog;

namespace AgroControl.Application.Stock;

public sealed record StockPolicyRecord(
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    int VersionNumber,
    DateTimeOffset UpdatedAt);

public sealed record WarehouseRecord(
    Guid WarehouseId,
    Guid OrganizationId,
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StockMovementRecord(
    Guid StockMovementId,
    WarehouseRecord Warehouse,
    Guid ProductId,
    string MovementType,
    decimal Quantity,
    decimal QuantityDelta,
    decimal ResultingQuantity,
    string Reason,
    string? ReferenceDocument,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);

public sealed record StockItemRecord(
    WarehouseRecord Warehouse,
    Guid ProductId,
    Guid OrganizationId,
    string Name,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool IsActive,
    bool AllowsFraction,
    ProductCategoryRecord? Category,
    BrandRecord? Brand,
    UnitOfMeasureRecord BaseUnit,
    decimal OnHandQuantity,
    bool IsLowStock,
    DateTimeOffset? LastMovementAt,
    StockPolicyRecord Policy);

public sealed record StockListQuery(
    string? Search,
    Guid? WarehouseId,
    Guid? CategoryId,
    Guid? BrandId,
    bool? IsLowStock,
    int Page,
    int PageSize);

public sealed record StockListResult(
    IReadOnlyList<StockItemRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record StockMovementListResult(
    IReadOnlyList<StockMovementRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record UpdateStockPolicyCommand(
    Guid WarehouseId,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint);

public sealed record CreateStockMovementCommand(
    Guid WarehouseId,
    Guid ProductId,
    string MovementType,
    decimal Quantity,
    string Reason,
    string? ReferenceDocument,
    string? Notes);

public sealed record CreateWarehouseCommand(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);

public sealed record UpdateWarehouseCommand(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);

public sealed record StockAlertRecord(
    WarehouseRecord Warehouse,
    Guid ProductId,
    string ProductName,
    string InternalCode,
    string UnitSymbol,
    decimal OnHandQuantity,
    decimal? ReorderPoint,
    decimal? MinQuantity,
    DateTimeOffset? LastMovementAt);

public sealed record PhysicalInventoryCountRecord(
    Guid PhysicalInventoryCountId,
    WarehouseRecord Warehouse,
    Guid ProductId,
    decimal ExpectedQuantity,
    decimal CountedQuantity,
    decimal DifferenceQuantity,
    string Reason,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);

public sealed record RecordPhysicalInventoryCountCommand(
    Guid WarehouseId,
    Guid ProductId,
    decimal CountedQuantity,
    string Reason,
    string? Notes);
