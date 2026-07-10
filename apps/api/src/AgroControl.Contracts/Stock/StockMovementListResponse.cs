namespace AgroControl.Contracts.Stock;

public sealed record StockMovementListResponse(
    IReadOnlyList<StockMovementResponse> Items,
    int Page,
    int PageSize,
    int Total);
