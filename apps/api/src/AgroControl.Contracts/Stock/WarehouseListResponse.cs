namespace AgroControl.Contracts.Stock;

public sealed record WarehouseListResponse(
    IReadOnlyList<WarehouseResponse> Items);
