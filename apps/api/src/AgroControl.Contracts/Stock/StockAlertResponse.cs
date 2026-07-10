namespace AgroControl.Contracts.Stock;

public sealed record StockAlertResponse(
    WarehouseResponse Warehouse,
    Guid ProductId,
    string ProductName,
    string InternalCode,
    string UnitSymbol,
    decimal OnHandQuantity,
    decimal? ReorderPoint,
    decimal? MinQuantity,
    DateTimeOffset? LastMovementAt);
