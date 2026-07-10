namespace AgroControl.Application.Cash;

public sealed record CashRegisterRecord(
    Guid CashRegisterId,
    Guid OrganizationId,
    string Name,
    string Code,
    bool IsActive);

public sealed record CashSessionRecord(
    Guid CashSessionId,
    Guid OrganizationId,
    CashRegisterRecord CashRegister,
    Guid OpenedByUserId,
    Guid? ClosedByUserId,
    decimal OpeningAmount,
    decimal? ClosingAmount,
    decimal? DifferenceAmount,
    decimal CurrentBalance,
    string Status,
    string? OpeningNotes,
    string? ClosingNotes,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public sealed record CashMovementRecord(
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

public sealed record CashOverviewRecord(
    CashRegisterRecord CashRegister,
    CashSessionRecord? CurrentSession,
    IReadOnlyList<CashMovementRecord> RecentMovements);

public sealed record CashMovementListResult(
    IReadOnlyList<CashMovementRecord> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record OpenCashSessionCommand(
    string CashRegisterCode,
    decimal OpeningAmount,
    string? OpeningNotes);

public sealed record CreateCashMovementCommand(
    Guid CashSessionId,
    string MovementType,
    string CategoryCode,
    string Concept,
    string PaymentMethod,
    decimal Amount,
    string? ReferenceDocument,
    string? Notes);

public sealed record CloseCashSessionCommand(
    decimal ClosingAmount,
    string? ClosingNotes);
