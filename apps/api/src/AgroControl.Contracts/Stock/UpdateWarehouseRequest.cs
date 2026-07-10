namespace AgroControl.Contracts.Stock;

public sealed record UpdateWarehouseRequest(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
