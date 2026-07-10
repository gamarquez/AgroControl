namespace AgroControl.Contracts.Catalog;

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    bool IsActive);
