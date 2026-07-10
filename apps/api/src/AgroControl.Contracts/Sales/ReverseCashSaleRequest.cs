namespace AgroControl.Contracts.Sales;

public sealed record ReverseCashSaleRequest(
    Guid CashSessionId,
    string? ReversalNotes);
