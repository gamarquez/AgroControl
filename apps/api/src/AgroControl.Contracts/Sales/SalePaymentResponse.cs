namespace AgroControl.Contracts.Sales;

public sealed record SalePaymentResponse(
    Guid SalePaymentId,
    string PaymentMethod,
    decimal Amount,
    string? Reference,
    string? ProviderName,
    DateTimeOffset CreatedAt);
