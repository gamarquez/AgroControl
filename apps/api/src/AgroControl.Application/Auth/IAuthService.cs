namespace AgroControl.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    Task LogoutAsync(IdentityContext identity, string refreshToken, CancellationToken cancellationToken);

    Task<AuthenticatedUser?> GetCurrentUserAsync(IdentityContext identity, CancellationToken cancellationToken);
}
