namespace AgroControl.Contracts.Catalog;

public sealed record CreateBrandRequest(
    string Name,
    string? Description);
