namespace AgroControl.Contracts.Stock;

public sealed record PhysicalInventoryCountResponse(
    Guid PhysicalInventoryCountId,
    WarehouseResponse Warehouse,
    Guid ProductId,
    decimal ExpectedQuantity,
    decimal CountedQuantity,
    decimal DifferenceQuantity,
    string Reason,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);
