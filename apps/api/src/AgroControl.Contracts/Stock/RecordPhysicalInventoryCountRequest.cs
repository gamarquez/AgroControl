namespace AgroControl.Contracts.Stock;

public sealed record RecordPhysicalInventoryCountRequest(
    Guid WarehouseId,
    Guid ProductId,
    decimal CountedQuantity,
    string Reason,
    string? Notes);
