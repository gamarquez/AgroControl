namespace AgroControl.Contracts.Catalog;

public sealed record PriceListListResponse(
    IReadOnlyList<PriceListResponse> Items);
