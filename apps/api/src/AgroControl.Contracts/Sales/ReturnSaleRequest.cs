namespace AgroControl.Contracts.Sales;

public sealed record ReturnSaleItemRequest(
    Guid SaleItemId,
    decimal Quantity);

public sealed record ReturnSaleRequest(
    Guid? CashSessionId,
    IReadOnlyList<ReturnSaleItemRequest> Items,
    string? Notes);
