namespace AgroControl.Contracts.Sales;

public sealed record SaleSummaryResponse(
    Guid SaleId,
    long TicketNumber,
    Guid? CashSessionId,
    Guid? CustomerId,
    string CustomerName,
    string SaleChannel,
    string Status,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal AccountBalanceAmount,
    decimal CreditBalanceAppliedAmount,
    DateOnly? DueDate,
    string CurrencyCode,
    int ItemCount,
    DateTimeOffset CreatedAt);
