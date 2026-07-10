namespace AgroControl.Contracts.Catalog;

public sealed record ProductListResponse(
    IReadOnlyList<ProductSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);
