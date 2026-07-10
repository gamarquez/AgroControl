namespace AgroControl.Contracts.Catalog;

public sealed record ProductCategoryResponse(
    Guid CategoryId,
    string Name,
    string? Description,
    bool IsActive);
