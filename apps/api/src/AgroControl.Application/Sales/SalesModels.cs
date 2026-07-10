namespace AgroControl.Application.Sales;

public sealed record PosProductRecord(
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

public sealed record PosProductListResult(
    IReadOnlyList<PosProductRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SaleItemRecord(
    Guid SaleItemId,
    Guid ProductId,
    string ProductName,
    string UnitSymbol,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal AvailableToReturnQuantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SalePaymentRecord(
    Guid SalePaymentId,
    string PaymentMethod,
    decimal Amount,
    string? Reference,
    string? ProviderName,
    DateTimeOffset CreatedAt);

public sealed record SaleReturnItemRecord(
    Guid SaleReturnItemId,
    Guid SaleItemId,
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    DateTimeOffset CreatedAt);

public sealed record SaleReturnRecord(
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
    IReadOnlyList<SaleReturnItemRecord> Items);

public sealed record SaleRecord(
    Guid SaleId,
    long TicketNumber,
    Guid OrganizationId,
    Guid? CashSessionId,
    Guid? CustomerId,
    Guid SoldByUserId,
    string SaleChannel,
    string Status,
    string CustomerName,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal AccountBalanceAmount,
    decimal CreditBalanceAppliedAmount,
    DateOnly? DueDate,
    string CurrencyCode,
    Guid? ReversalCashSessionId,
    Guid? ReversedByUserId,
    DateTimeOffset? ReversedAt,
    string? Notes,
    string? ReversalNotes,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SaleItemRecord> Items,
    IReadOnlyList<SalePaymentRecord> Payments,
    IReadOnlyList<SaleReturnRecord> Returns);

public sealed record SaleSummaryRecord(
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

public sealed record SaleListResult(
    IReadOnlyList<SaleSummaryRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record SaleListQuery(
    string? Search,
    string? Status,
    Guid? CustomerId,
    int Page,
    int PageSize);

public sealed record PosProductListQuery(
    string? Search,
    int Page,
    int PageSize);

public sealed record CreateSaleItemCommand(
    Guid ProductId,
    decimal Quantity);

public sealed record CreateCashSaleCommand(
    Guid CashSessionId,
    IReadOnlyList<CreateSaleItemCommand> Items,
    string? Notes);

public sealed record CreateAccountSaleCommand(
    Guid CustomerId,
    IReadOnlyList<CreateSaleItemCommand> Items,
    DateOnly? DueDate,
    string? Notes);

public sealed record CreateSalePaymentCommand(
    string PaymentMethod,
    decimal Amount,
    string? Reference,
    string? ProviderName);

public sealed record CreateCheckoutSaleCommand(
    Guid? CashSessionId,
    Guid? CustomerId,
    IReadOnlyList<CreateSaleItemCommand> Items,
    IReadOnlyList<CreateSalePaymentCommand> Payments,
    DateOnly? DueDate,
    string? Notes);

public sealed record ReverseCashSaleCommand(
    Guid CashSessionId,
    string? ReversalNotes);

public sealed record ReturnSaleItemCommand(
    Guid SaleItemId,
    decimal Quantity);

public sealed record ReturnSaleCommand(
    Guid? CashSessionId,
    IReadOnlyList<ReturnSaleItemCommand> Items,
    string? Notes);
