namespace AgroControl.Contracts.Customers;

public sealed record CreateCustomerRequest(
    string DisplayName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimitAmount,
    string? Notes);
