namespace AgroControl.Application.Customers;

public sealed record CustomerRecord(
    Guid CustomerId,
    Guid OrganizationId,
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

public sealed record CustomerAccountMovementRecord(
    Guid CustomerAccountMovementId,
    Guid CustomerId,
    Guid? SaleId,
    Guid? CashSessionId,
    string MovementType,
    string Concept,
    string? ReferenceDocument,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal OpenAmount,
    bool IsOverdue,
    DateOnly? DueDate,
    decimal ResultingBalance,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);

public sealed record CustomerAccountStatementRecord(
    CustomerRecord Customer,
    IReadOnlyList<CustomerAccountMovementRecord> Movements);

public sealed record CustomerListResult(
    IReadOnlyList<CustomerRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CustomerListQuery(
    string? Search,
    bool? IsActive,
    int Page,
    int PageSize);

public sealed record CustomerAccountMovementListResult(
    IReadOnlyList<CustomerAccountMovementRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CreateCustomerCommand(
    string DisplayName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimitAmount,
    string? Notes);

public sealed record UpdateCustomerCommand(
    string DisplayName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Address,
    decimal CreditLimitAmount,
    bool IsActive,
    string? Notes);

public sealed record RecordCustomerPaymentCommand(
    Guid CashSessionId,
    decimal Amount,
    string? Notes);

public sealed record RecordCustomerCreditNoteCommand(
    decimal Amount,
    string Concept,
    string? ReferenceDocument,
    string? Notes);
