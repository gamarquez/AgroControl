namespace AgroControl.Contracts.Customers;

public sealed record UpdateCustomerRequest(
    string DisplayName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimitAmount,
    bool IsActive,
    string? Notes);
