namespace AgroControl.Application.Auth;

public interface IAuthRepository
{
    Task<AuthenticatedUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<AuthenticatedUser?> FindByIdAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);

    Task<RefreshSessionRecord?> FindRefreshSessionAsync(Guid sessionId, string tokenHash, CancellationToken cancellationToken);

    Task CreateRefreshSessionAsync(RefreshSessionRecord session, CancellationToken cancellationToken);

    Task RotateRefreshSessionAsync(
        Guid currentSessionId,
        string currentTokenHash,
        RefreshSessionRecord replacementSession,
        CancellationToken cancellationToken);

    Task RevokeRefreshSessionAsync(Guid sessionId, string tokenHash, string reason, CancellationToken cancellationToken);

    Task RecordLoginAsync(Guid userId, Guid organizationId, DateTimeOffset loggedAt, CancellationToken cancellationToken);
}
