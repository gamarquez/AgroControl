namespace AgroControl.Contracts.Cash;

public sealed record CashSessionResponse(
    Guid CashSessionId,
    CashRegisterResponse CashRegister,
    decimal OpeningAmount,
    decimal? ClosingAmount,
    decimal? DifferenceAmount,
    decimal CurrentBalance,
    string Status,
    string? OpeningNotes,
    string? ClosingNotes,
    Guid OpenedByUserId,
    Guid? ClosedByUserId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);
