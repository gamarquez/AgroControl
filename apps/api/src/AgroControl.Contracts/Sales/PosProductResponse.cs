namespace AgroControl.Contracts.Sales;

public sealed record PosProductResponse(
    Guid ProductId,
    string Name,
    string InternalCode,
    string? Sku,
    string? Barcode,
    bool AllowsFraction,
    string UnitSymbol,
    decimal OnHandQuantity,
    decimal SaleAmount,
    string CurrencyCode);
