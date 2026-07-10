namespace AgroControl.Contracts.Customers;

public sealed record RecordCustomerPaymentRequest(
    Guid CashSessionId,
    decimal Amount,
    string? Notes);
