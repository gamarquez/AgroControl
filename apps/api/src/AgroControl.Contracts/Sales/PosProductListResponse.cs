namespace AgroControl.Contracts.Sales;

public sealed record PosProductListResponse(
    IReadOnlyList<PosProductResponse> Items,
    int Page,
    int PageSize,
    int Total);
