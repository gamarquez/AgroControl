namespace AgroControl.Contracts.Catalog;

public sealed record ProductCategoryListResponse(
    IReadOnlyList<ProductCategoryResponse> Items);
