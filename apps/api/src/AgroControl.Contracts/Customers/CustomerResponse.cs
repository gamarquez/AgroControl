namespace AgroControl.Contracts.Customers;

public sealed record CustomerResponse(
    Guid CustomerId,
    string DisplayName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimitAmount,
    decimal CurrentBalance,
    decimal OverdueBalance,
    DateOnly? NextDueDate,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
