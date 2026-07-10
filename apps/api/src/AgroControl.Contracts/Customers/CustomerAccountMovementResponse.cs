namespace AgroControl.Contracts.Customers;

public sealed record CustomerAccountMovementResponse(
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
