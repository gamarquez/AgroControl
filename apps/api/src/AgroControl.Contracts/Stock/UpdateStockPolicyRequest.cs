namespace AgroControl.Contracts.Stock;

public sealed record UpdateStockPolicyRequest(
    Guid WarehouseId,
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint);
