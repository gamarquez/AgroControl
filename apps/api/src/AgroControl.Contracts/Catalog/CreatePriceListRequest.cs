namespace AgroControl.Contracts.Catalog;

public sealed record CreatePriceListRequest(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
