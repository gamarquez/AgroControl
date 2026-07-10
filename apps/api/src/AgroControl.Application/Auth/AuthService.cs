namespace AgroControl.Application.Auth;

internal sealed class AuthService(
    IAuthRepository authRepository,
    IAuditLogRepository auditLogRepository,
    IPasswordHasher passwordHasher,
    IRefreshTokenProtector refreshTokenProtector,
    ITokenService tokenService,
    TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = AuthValidation.NormalizeEmail(email);
        AuthValidation.ValidatePassword(password);

        var user = await authRepository.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            await WriteAuditAsync(
                Guid.Empty,
                null,
                "auth",
                normalizedEmail,
                "login_failed",
                new Dictionary<string, object?> { ["reason"] = "invalid_credentials" },
                cancellationToken);

            throw new AuthenticationException("Credenciales inválidas.");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationException("El usuario está inactivo.");
        }

        if (user.IsLocked)
        {
            throw new AuthenticationException("El usuario está bloqueado.");
        }

        if (passwordHasher.Verify(password, user.PasswordHash) == PasswordVerificationResult.Failed)
        {
            await WriteAuditAsync(
                user.OrganizationId,
                user.UserId,
                "auth",
                user.UserId.ToString(),
                "login_failed",
                new Dictionary<string, object?> { ["reason"] = "invalid_credentials" },
                cancellationToken);

            throw new AuthenticationException("Credenciales inválidas.");
        }

        var tokens = tokenService.IssueTokens(user);
        var refreshHash = refreshTokenProtector.Hash(tokens.RefreshToken);

        await authRepository.CreateRefreshSessionAsync(
            new RefreshSessionRecord(
                tokens.SessionId,
                user.UserId,
                user.OrganizationId,
                refreshHash,
                tokens.RefreshTokenExpiresAt,
                timeProvider.GetUtcNow(),
                null,
                null),
            cancellationToken);

        await authRepository.RecordLoginAsync(user.UserId, user.OrganizationId, timeProvider.GetUtcNow(), cancellationToken);
        await WriteAuditAsync(
            user.OrganizationId,
            user.UserId,
            "auth",
            user.UserId.ToString(),
            "login_succeeded",
            new Dictionary<string, object?> { ["sessionId"] = tokens.SessionId },
            cancellationToken);

        return new AuthResult(user, tokens);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new AuthenticationException("El refresh token es obligatorio.");
        }

        if (!TryReadSessionId(refreshToken, out var sessionId))
        {
            throw new AuthenticationException("El refresh token no es válido.");
        }

        var refreshHash = refreshTokenProtector.Hash(refreshToken);
        var currentSession = await authRepository.FindRefreshSessionAsync(sessionId, refreshHash, cancellationToken);

        if (currentSession is null || currentSession.RevokedAt is not null || currentSession.ExpiresAt <= timeProvider.GetUtcNow())
        {
            throw new AuthenticationException("El refresh token ya no es válido.");
        }

        var user = await authRepository.FindByIdAsync(
            currentSession.UserId,
            currentSession.OrganizationId,
            cancellationToken);

        if (user is null || !user.IsActive || user.IsLocked)
        {
            throw new AuthenticationException("La sesión ya no es válida para el usuario actual.");
        }

        var tokens = tokenService.IssueTokens(user);
        var replacementHash = refreshTokenProtector.Hash(tokens.RefreshToken);

        await authRepository.RotateRefreshSessionAsync(
            currentSession.SessionId,
            refreshHash,
            new RefreshSessionRecord(
                tokens.SessionId,
                user.UserId,
                user.OrganizationId,
                replacementHash,
                tokens.RefreshTokenExpiresAt,
                timeProvider.GetUtcNow(),
                null,
                currentSession.SessionId),
            cancellationToken);

        await WriteAuditAsync(
            user.OrganizationId,
            user.UserId,
            "auth",
            user.UserId.ToString(),
            "refresh_succeeded",
            new Dictionary<string, object?> { ["sessionId"] = tokens.SessionId },
            cancellationToken);

        return new AuthResult(user, tokens);
    }

    public async Task LogoutAsync(IdentityContext identity, string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ValidationException("El refresh token es obligatorio para cerrar sesión.");
        }

        if (!TryReadSessionId(refreshToken, out var sessionId))
        {
            throw new AuthenticationException("El refresh token no es válido.");
        }

        await authRepository.RevokeRefreshSessionAsync(
            sessionId,
            refreshTokenProtector.Hash(refreshToken),
            "logout",
            cancellationToken);

        await WriteAuditAsync(
            identity.OrganizationId,
            identity.UserId,
            "auth",
            identity.UserId.ToString(),
            "logout_succeeded",
            new Dictionary<string, object?> { ["sessionId"] = sessionId },
            cancellationToken);
    }

    public Task<AuthenticatedUser?> GetCurrentUserAsync(IdentityContext identity, CancellationToken cancellationToken)
    {
        return authRepository.FindByIdAsync(identity.UserId, identity.OrganizationId, cancellationToken);
    }

    private async Task WriteAuditAsync(
        Guid organizationId,
        Guid? actorUserId,
        string entityName,
        string entityId,
        string action,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty)
        {
            return;
        }

        await auditLogRepository.WriteAsync(
            organizationId,
            actorUserId,
            entityName,
            entityId,
            action,
            metadata,
            cancellationToken);
    }

    private static bool TryReadSessionId(string refreshToken, out Guid sessionId)
    {
        sessionId = Guid.Empty;
        var parts = refreshToken.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 2 && Guid.TryParse(parts[0], out sessionId);
    }
}
