namespace AgroControl.Contracts.Catalog;

public sealed record BrandResponse(
    Guid BrandId,
    string Name,
    string? Description,
    bool IsActive);
