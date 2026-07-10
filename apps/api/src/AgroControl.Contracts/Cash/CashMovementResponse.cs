namespace AgroControl.Contracts.Cash;

public sealed record CashMovementResponse(
    Guid CashMovementId,
    Guid CashSessionId,
    string MovementType,
    string CategoryCode,
    string Concept,
    string PaymentMethod,
    decimal Amount,
    decimal SignedAmount,
    decimal ResultingBalance,
    string? ReferenceDocument,
    string? Notes,
    Guid? PerformedByUserId,
    DateTimeOffset CreatedAt);
