namespace AgroControl.Contracts.Catalog;

public sealed record UnitOfMeasureListResponse(
    IReadOnlyList<UnitOfMeasureResponse> Items);
