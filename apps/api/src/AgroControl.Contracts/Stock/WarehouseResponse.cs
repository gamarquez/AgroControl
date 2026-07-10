namespace AgroControl.Contracts.Stock;

public sealed record WarehouseResponse(
    Guid WarehouseId,
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
