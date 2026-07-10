namespace AgroControl.Contracts.Sales;

public sealed record SaleReturnItemResponse(
    Guid SaleReturnItemId,
    Guid SaleItemId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    DateTimeOffset CreatedAt);
