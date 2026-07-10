namespace AgroControl.Contracts.Cash;

public sealed record OpenCashSessionRequest(
    string CashRegisterCode,
    decimal OpeningAmount,
    string? OpeningNotes);
