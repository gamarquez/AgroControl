namespace AgroControl.Contracts.Catalog;

public sealed record PriceListResponse(
    Guid PriceListId,
    string Name,
    string Code,
    bool IsDefault,
    bool IsActive);
