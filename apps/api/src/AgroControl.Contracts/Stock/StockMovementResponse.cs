namespace AgroControl.Contracts.Stock;

public sealed record StockMovementResponse(
    Guid StockMovementId,
    WarehouseResponse Warehouse,
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
