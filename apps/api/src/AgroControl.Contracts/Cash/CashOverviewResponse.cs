namespace AgroControl.Contracts.Cash;

public sealed record CashOverviewResponse(
    CashRegisterResponse CashRegister,
    CashSessionResponse? CurrentSession,
    IReadOnlyList<CashMovementResponse> RecentMovements);
