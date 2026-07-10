namespace AgroControl.Contracts.Cash;

public sealed record CashRegisterResponse(
    Guid CashRegisterId,
    string Name,
    string Code,
    bool IsActive);
