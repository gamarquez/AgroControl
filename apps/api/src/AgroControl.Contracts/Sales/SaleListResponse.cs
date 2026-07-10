namespace AgroControl.Contracts.Sales;

public sealed record SaleListResponse(
    IReadOnlyList<SaleSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);
