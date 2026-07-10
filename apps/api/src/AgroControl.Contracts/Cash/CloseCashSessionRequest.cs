namespace AgroControl.Contracts.Cash;

public sealed record CloseCashSessionRequest(
    decimal ClosingAmount,
    string? ClosingNotes);
