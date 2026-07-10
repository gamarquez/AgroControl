using AgroControl.Application.Auth;

namespace AgroControl.Api.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_IssuesTokensAndPersistsSession()
    {
        var user = CreateUser();
        var authRepository = new FakeAuthRepository { UserByEmail = user };
        var auditRepository = new FakeAuditLogRepository();
        var tokenService = new FakeTokenService();
        var service = new AuthService(
            authRepository,
            auditRepository,
            new FakePasswordHasher(PasswordVerificationResult.Success),
            new FakeRefreshTokenProtector(),
            tokenService,
            TimeProvider.System);

        var result = await service.LoginAsync(user.Email, "correct-password", CancellationToken.None);

        Assert.Equal(user.UserId, result.User.UserId);
        Assert.Equal(tokenService.TokenPair.AccessToken, result.Tokens.AccessToken);
        Assert.NotNull(authRepository.CreatedSession);
        Assert.Equal(user.UserId, authRepository.LastRecordedLoginUserId);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "login_succeeded");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ThrowsAuthenticationException()
    {
        var user = CreateUser();
        var authRepository = new FakeAuthRepository { UserByEmail = user };
        var auditRepository = new FakeAuditLogRepository();
        var service = new AuthService(
            authRepository,
            auditRepository,
            new FakePasswordHasher(PasswordVerificationResult.Failed),
            new FakeRefreshTokenProtector(),
            new FakeTokenService(),
            TimeProvider.System);

        await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync(user.Email, "bad-password", CancellationToken.None));
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "login_failed");
    }

    [Fact]
    public async Task LoginAsync_WithLockedUser_ThrowsAuthenticationException()
    {
        var authRepository = new FakeAuthRepository
        {
            UserByEmail = CreateUser(isLocked: true)
        };
        var service = new AuthService(
            authRepository,
            new FakeAuditLogRepository(),
            new FakePasswordHasher(PasswordVerificationResult.Success),
            new FakeRefreshTokenProtector(),
            new FakeTokenService(),
            TimeProvider.System);

        await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync("locked@demo.local", "correct-password", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_WithRevokedSession_ThrowsAuthenticationException()
    {
        var sessionId = Guid.NewGuid();
        var authRepository = new FakeAuthRepository
        {
            RefreshSession = new RefreshSessionRecord(
                sessionId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "hash-refresh-token",
                DateTimeOffset.UtcNow.AddMinutes(10),
                DateTimeOffset.UtcNow.AddMinutes(-10),
                DateTimeOffset.UtcNow.AddMinutes(-1),
                null)
        };
        var service = new AuthService(
            authRepository,
            new FakeAuditLogRepository(),
            new FakePasswordHasher(PasswordVerificationResult.Success),
            new FakeRefreshTokenProtector(),
            new FakeTokenService(),
            TimeProvider.System);

        await Assert.ThrowsAsync<AuthenticationException>(() => service.RefreshAsync($"{sessionId:N}.refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task LogoutAsync_RevokesRefreshSession()
    {
        var sessionId = Guid.NewGuid();
        var authRepository = new FakeAuthRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new AuthService(
            authRepository,
            auditRepository,
            new FakePasswordHasher(PasswordVerificationResult.Success),
            new FakeRefreshTokenProtector(),
            new FakeTokenService(),
            TimeProvider.System);

        await service.LogoutAsync(
            new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), sessionId, "admin@demo.local", ["administrator"]),
            $"{sessionId:N}.refresh-token",
            CancellationToken.None);

        Assert.Equal(sessionId, authRepository.RevokedSessionId);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "logout_succeeded");
    }

    [Fact]
    public async Task UserManagementService_RequiresAdministratorRole()
    {
        var service = new UserManagementService(
            new FakeUserManagementRepository(),
            new FakeAuditLogRepository(),
            new FakePasswordHasher(PasswordVerificationResult.Success));

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.ListUsersAsync(
                new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), null, "viewer@demo.local", ["viewer"]),
                CancellationToken.None));
    }

    [Fact]
    public async Task UpdateOrganizationSettingsAsync_UsesOrganizationScopeFromIdentity()
    {
        var repository = new FakeUserManagementRepository();
        var service = new UserManagementService(
            repository,
            new FakeAuditLogRepository(),
            new FakePasswordHasher(PasswordVerificationResult.Success));
        var organizationId = Guid.NewGuid();

        var result = await service.UpdateOrganizationSettingsAsync(
            new IdentityContext(Guid.NewGuid(), organizationId, null, "admin@demo.local", ["administrator"]),
            new OrganizationSettingsRecord(Guid.Empty, "Legal Name", "Trade Name", "30-12345678-9", "America/Argentina/Buenos_Aires", "ARS"),
            CancellationToken.None);

        Assert.Equal(organizationId, result.OrganizationId);
        Assert.Equal(organizationId, repository.UpdatedSettings?.OrganizationId);
    }

    private static AuthenticatedUser CreateUser(bool isLocked = false)
    {
        var organizationId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        return new AuthenticatedUser(
            Guid.NewGuid(),
            organizationId,
            "admin@demo.local",
            "Admin Demo",
            true,
            isLocked,
            false,
            "password-hash",
            [new RoleAssignment(roleId, "administrator", "Administrador")],
            ["users:write"]);
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public AuthenticatedUser? UserByEmail { get; init; }

        public AuthenticatedUser? UserById { get; init; }

        public RefreshSessionRecord? RefreshSession { get; init; }

        public RefreshSessionRecord? CreatedSession { get; private set; }

        public Guid? RevokedSessionId { get; private set; }

        public Guid? LastRecordedLoginUserId { get; private set; }

        public Task<AuthenticatedUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
            => Task.FromResult(UserByEmail);

        public Task<AuthenticatedUser?> FindByIdAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult(UserById ?? UserByEmail);

        public Task<RefreshSessionRecord?> FindRefreshSessionAsync(Guid sessionId, string tokenHash, CancellationToken cancellationToken)
            => Task.FromResult(RefreshSession);

        public Task CreateRefreshSessionAsync(RefreshSessionRecord session, CancellationToken cancellationToken)
        {
            CreatedSession = session;
            return Task.CompletedTask;
        }

        public Task RotateRefreshSessionAsync(Guid currentSessionId, string currentTokenHash, RefreshSessionRecord replacementSession, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RevokeRefreshSessionAsync(Guid sessionId, string tokenHash, string reason, CancellationToken cancellationToken)
        {
            RevokedSessionId = sessionId;
            return Task.CompletedTask;
        }

        public Task RecordLoginAsync(Guid userId, Guid organizationId, DateTimeOffset loggedAt, CancellationToken cancellationToken)
        {
            LastRecordedLoginUserId = userId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserManagementRepository : IUserManagementRepository
    {
        public OrganizationSettingsRecord? UpdatedSettings { get; private set; }

        public Task<IReadOnlyList<AuthenticatedUser>> ListUsersAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AuthenticatedUser>>([]);

        public Task<IReadOnlyList<RoleAssignment>> ListRolesAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RoleAssignment>>([]);

        public Task<Guid> CreateUserAsync(Guid organizationId, CreateUserCommand command, string passwordHash, CancellationToken cancellationToken)
            => Task.FromResult(Guid.NewGuid());

        public Task<AuthenticatedUser?> UpdateUserAsync(Guid organizationId, Guid userId, UpdateUserCommand command, CancellationToken cancellationToken)
            => Task.FromResult<AuthenticatedUser?>(CreateUser());

        public Task<OrganizationSettingsRecord?> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<OrganizationSettingsRecord?>(new OrganizationSettingsRecord(organizationId, "Legal", "Trade", "30-12345678-9", "America/Argentina/Buenos_Aires", "ARS"));

        public Task<OrganizationSettingsRecord> UpdateOrganizationSettingsAsync(OrganizationSettingsRecord settings, CancellationToken cancellationToken)
        {
            UpdatedSettings = settings;
            return Task.FromResult(settings);
        }
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<(string Action, string EntityName)> Entries { get; } = [];

        public Task WriteAsync(Guid organizationId, Guid? actorUserId, string entityName, string entityId, string action, IReadOnlyDictionary<string, object?> metadata, CancellationToken cancellationToken)
        {
            Entries.Add((action, entityName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher(PasswordVerificationResult result) : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";

        public PasswordVerificationResult Verify(string password, string hash) => result;
    }

    private sealed class FakeRefreshTokenProtector : IRefreshTokenProtector
    {
        public string GenerateToken() => "refresh-token";

        public string Hash(string token) => $"hash-{token}";
    }

    private sealed class FakeTokenService : ITokenService
    {
        public TokenPair TokenPair { get; } = new(
            "access-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh-token",
            DateTimeOffset.UtcNow.AddDays(14),
            Guid.NewGuid());

        public TokenPair IssueTokens(AuthenticatedUser user) => TokenPair;

        public IdentityContext? ReadIdentity(System.Security.Claims.ClaimsPrincipal principal) => null;
    }
}
