using AgroControl.Application.Auth;

namespace AgroControl.Application.Cash;

public interface ICashService
{
    Task<CashOverviewRecord> GetOverviewAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<CashMovementListResult> ListMovementsAsync(IdentityContext identity, int page, int pageSize, CancellationToken cancellationToken);

    Task<CashSessionRecord> OpenSessionAsync(IdentityContext identity, OpenCashSessionCommand command, CancellationToken cancellationToken);

    Task<CashMovementRecord> CreateMovementAsync(IdentityContext identity, CreateCashMovementCommand command, CancellationToken cancellationToken);

    Task<CashSessionRecord> CloseSessionAsync(IdentityContext identity, Guid cashSessionId, CloseCashSessionCommand command, CancellationToken cancellationToken);
}
