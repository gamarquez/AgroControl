namespace AgroControl.Contracts.Catalog;

public sealed record CreateCategoryRequest(
    string Name,
    string? Description);
