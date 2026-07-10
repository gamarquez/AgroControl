namespace AgroControl.Application.Cash;

public interface ICashRepository
{
    Task<CashOverviewRecord> GetOverviewAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<CashMovementListResult> ListMovementsAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken);

    Task<CashSessionRecord?> GetSessionAsync(Guid organizationId, Guid cashSessionId, CancellationToken cancellationToken);

    Task<CashSessionRecord> OpenSessionAsync(Guid organizationId, Guid actorUserId, OpenCashSessionCommand command, CancellationToken cancellationToken);

    Task<CashMovementRecord> CreateMovementAsync(Guid organizationId, Guid actorUserId, CreateCashMovementCommand command, CancellationToken cancellationToken);

    Task<CashSessionRecord> CloseSessionAsync(Guid organizationId, Guid actorUserId, Guid cashSessionId, CloseCashSessionCommand command, CancellationToken cancellationToken);
}
