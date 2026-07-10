namespace AgroControl.Contracts.Stock;

public sealed record CreateWarehouseRequest(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
