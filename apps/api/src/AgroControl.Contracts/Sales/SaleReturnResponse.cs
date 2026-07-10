namespace AgroControl.Contracts.Sales;

public sealed record SaleReturnResponse(
    Guid SaleReturnId,
    Guid SaleId,
    Guid? CashSessionId,
    Guid? CustomerAccountMovementId,
    Guid ReturnedByUserId,
    decimal ReturnTotalAmount,
    decimal RefundedPaidAmount,
    decimal CreditedAccountAmount,
    string? Notes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SaleReturnItemResponse> Items);
