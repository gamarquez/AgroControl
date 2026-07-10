namespace AgroControl.Contracts.Cash;

public sealed record CashMovementListResponse(
    IReadOnlyList<CashMovementResponse> Items,
    int Page,
    int PageSize,
    int Total);
