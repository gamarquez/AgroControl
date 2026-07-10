namespace AgroControl.Contracts.Stock;

public sealed record StockListResponse(
    IReadOnlyList<StockSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);
