namespace AgroControl.Contracts.Stock;

public sealed record StockPolicyResponse(
    decimal? MinQuantity,
    decimal? MaxQuantity,
    decimal? ReorderPoint,
    int VersionNumber,
    DateTimeOffset UpdatedAt);
