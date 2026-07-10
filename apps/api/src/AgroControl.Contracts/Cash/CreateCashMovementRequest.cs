namespace AgroControl.Contracts.Cash;

public sealed record CreateCashMovementRequest(
    Guid CashSessionId,
    string MovementType,
    string CategoryCode,
    string Concept,
    string PaymentMethod,
    decimal Amount,
    string? ReferenceDocument,
    string? Notes);
