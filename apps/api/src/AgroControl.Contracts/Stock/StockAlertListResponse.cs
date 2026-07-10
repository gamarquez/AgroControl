namespace AgroControl.Contracts.Stock;

public sealed record StockAlertListResponse(
    IReadOnlyList<StockAlertResponse> Items);
