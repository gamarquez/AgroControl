namespace AgroControl.Contracts.Sales;

public sealed record CreateCashSaleRequest(
    Guid CashSessionId,
    IReadOnlyList<CreateCashSaleItemRequest> Items,
    string? Notes);

public sealed record CreateCashSaleItemRequest(
    Guid ProductId,
    decimal Quantity);

public sealed record CreateAccountSaleRequest(
    Guid CustomerId,
    IReadOnlyList<CreateCashSaleItemRequest> Items,
    DateOnly? DueDate,
    string? Notes);

public sealed record CreateSalePaymentRequest(
    string PaymentMethod,
    decimal Amount,
    string? Reference,
    string? ProviderName);

public sealed record CreateCheckoutSaleRequest(
    Guid? CashSessionId,
    Guid? CustomerId,
    IReadOnlyList<CreateCashSaleItemRequest> Items,
    IReadOnlyList<CreateSalePaymentRequest> Payments,
    DateOnly? DueDate,
    string? Notes);
