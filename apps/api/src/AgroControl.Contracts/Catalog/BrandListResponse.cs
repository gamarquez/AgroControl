namespace AgroControl.Contracts.Catalog;

public sealed record BrandListResponse(
    IReadOnlyList<BrandResponse> Items);
