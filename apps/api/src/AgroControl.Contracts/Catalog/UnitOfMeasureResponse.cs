namespace AgroControl.Contracts.Catalog;

public sealed record UnitOfMeasureResponse(
    Guid UnitId,
    string Name,
    string Code,
    string Symbol,
    bool AllowsFraction,
    bool IsActive);
