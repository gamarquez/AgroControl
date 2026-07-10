namespace AgroControl.Contracts.Catalog;

public sealed record UpdatePriceListRequest(
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
