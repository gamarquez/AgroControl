namespace AgroControl.Contracts.Catalog;

public sealed record UpdateBrandRequest(
    string Name,
    string? Description,
    bool IsActive);
