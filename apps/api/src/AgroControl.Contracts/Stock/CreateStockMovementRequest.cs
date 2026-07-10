namespace AgroControl.Contracts.Stock;

public sealed record CreateStockMovementRequest(
    Guid WarehouseId,
    Guid ProductId,
    string MovementType,
    decimal Quantity,
    string Reason,
    string? ReferenceDocument,
    string? Notes);
