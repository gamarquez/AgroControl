namespace AgroControl.Contracts.Sales;

public sealed record SaleItemResponse(
    Guid SaleItemId,
    Guid ProductId,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal AvailableToReturnQuantity,
    decimal UnitPrice,
    decimal LineTotal);
