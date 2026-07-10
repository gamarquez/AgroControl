using AgroControl.Application.Auth;
using AgroControl.Application.Persistence;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlAuthRepository(ISqlConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<AuthenticatedUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql =
            """
            select
                u.user_id,
                u.organization_id,
                u.email,
                u.display_name,
                u.is_active,
                coalesce(u.is_locked, false) as is_locked,
                coalesce(u.must_change_password, false) as must_change_password,
                u.password_hash,
                r.role_id,
                r.code as role_code,
                r.name as role_name,
                p.module as permission_module,
                p.action as permission_action
            from app.users u
            left join app.user_roles ur on ur.user_id = u.user_id
            left join app.roles r on r.role_id = ur.role_id
            left join app.role_permissions rp on rp.role_id = r.role_id
            left join app.permissions p on p.permission_id = rp.permission_id
            where lower(u.email) = @email
            order by u.created_at asc;
            """;

        return await FindUserAsync(sql, cancellationToken, ("email", email));
    }

    public async Task<AuthenticatedUser?> FindByIdAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            select
                u.user_id,
                u.organization_id,
                u.email,
                u.display_name,
                u.is_active,
                coalesce(u.is_locked, false) as is_locked,
                coalesce(u.must_change_password, false) as must_change_password,
                u.password_hash,
                r.role_id,
                r.code as role_code,
                r.name as role_name,
                p.module as permission_module,
                p.action as permission_action
            from app.users u
            left join app.user_roles ur on ur.user_id = u.user_id
            left join app.roles r on r.role_id = ur.role_id
            left join app.role_permissions rp on rp.role_id = r.role_id
            left join app.permissions p on p.permission_id = rp.permission_id
            where u.organization_id = @organization_id
              and u.user_id = @user_id;
            """;

        return await FindUserAsync(sql, cancellationToken, ("organization_id", organizationId), ("user_id", userId));
    }

    public async Task<RefreshSessionRecord?> FindRefreshSessionAsync(Guid sessionId, string tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select session_id, user_id, organization_id, token_hash, expires_at, created_at, revoked_at, replaced_by_session_id
            from app.refresh_sessions
            where session_id = @session_id
              and token_hash = @token_hash;
            """;
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("token_hash", tokenHash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RefreshSessionRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7));
    }

    public async Task CreateRefreshSessionAsync(RefreshSessionRecord session, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            insert into app.refresh_sessions (
                session_id,
                user_id,
                organization_id,
                token_hash,
                expires_at,
                created_at,
                revoked_at,
                replaced_by_session_id)
            values (
                @session_id,
                @user_id,
                @organization_id,
                @token_hash,
                @expires_at,
                @created_at,
                @revoked_at,
                @replaced_by_session_id);
            """;

        AddSessionParameters(command, session);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RotateRefreshSessionAsync(
        Guid currentSessionId,
        string currentTokenHash,
        RefreshSessionRecord replacementSession,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var revokeCommand = connection.CreateCommand())
        {
            revokeCommand.Transaction = transaction;
            revokeCommand.CommandText =
                """
                update app.refresh_sessions
                set revoked_at = timezone('utc', now()),
                    revoked_reason = 'rotated',
                    replaced_by_session_id = @replacement_session_id
                where session_id = @session_id
                  and token_hash = @token_hash
                  and revoked_at is null;
                """;
            revokeCommand.Parameters.AddWithValue("replacement_session_id", replacementSession.SessionId);
            revokeCommand.Parameters.AddWithValue("session_id", currentSessionId);
            revokeCommand.Parameters.AddWithValue("token_hash", currentTokenHash);

            var updated = await revokeCommand.ExecuteNonQueryAsync(cancellationToken);

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new AuthenticationException("El refresh token ya no es valido.");
            }
        }

        await using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                insert into app.refresh_sessions (
                    session_id,
                    user_id,
                    organization_id,
                    token_hash,
                    expires_at,
                    created_at,
                    revoked_at,
                    replaced_by_session_id)
                values (
                    @session_id,
                    @user_id,
                    @organization_id,
                    @token_hash,
                    @expires_at,
                    @created_at,
                    @revoked_at,
                    @replaced_by_session_id);
                """;

            AddSessionParameters(insertCommand, replacementSession);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeRefreshSessionAsync(Guid sessionId, string tokenHash, string reason, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            update app.refresh_sessions
            set revoked_at = timezone('utc', now()),
                revoked_reason = @reason
            where session_id = @session_id
              and token_hash = @token_hash
              and revoked_at is null;
            """;
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("token_hash", tokenHash);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordLoginAsync(Guid userId, Guid organizationId, DateTimeOffset loggedAt, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            update app.users
            set last_login_at = @logged_at
            where user_id = @user_id
              and organization_id = @organization_id;
            """;
        command.Parameters.AddWithValue("logged_at", loggedAt);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("organization_id", organizationId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AuthenticatedUser?> FindUserAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = await ReadUsersAsync(reader, cancellationToken);
        return users.FirstOrDefault();
    }

    private static void AddSessionParameters(NpgsqlCommand command, RefreshSessionRecord session)
    {
        command.Parameters.AddWithValue("session_id", session.SessionId);
        command.Parameters.AddWithValue("user_id", session.UserId);
        command.Parameters.AddWithValue("organization_id", session.OrganizationId);
        command.Parameters.AddWithValue("token_hash", session.TokenHash);
        command.Parameters.AddWithValue("expires_at", session.ExpiresAt);
        command.Parameters.AddWithValue("created_at", session.CreatedAt);
        command.Parameters.AddWithValue("revoked_at", (object?)session.RevokedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("replaced_by_session_id", (object?)session.ReplacedBySessionId ?? DBNull.Value);
    }

    internal static async Task<IReadOnlyList<AuthenticatedUser>> ReadUsersAsync(
        NpgsqlDataReader reader,
        CancellationToken cancellationToken)
    {
        var users = new Dictionary<Guid, UserAccumulator>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var userId = reader.GetGuid(0);

            if (!users.TryGetValue(userId, out var accumulator))
            {
                accumulator = new UserAccumulator(
                    userId,
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetBoolean(4),
                    reader.GetBoolean(5),
                    reader.GetBoolean(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7));
                users[userId] = accumulator;
            }

            if (!reader.IsDBNull(8))
            {
                accumulator.Roles.TryAdd(
                    reader.GetGuid(8),
                    new RoleAssignment(reader.GetGuid(8), reader.GetString(9), reader.GetString(10)));
            }

            if (!reader.IsDBNull(11))
            {
                accumulator.Permissions.Add($"{reader.GetString(11)}:{reader.GetString(12)}");
            }
        }

        return users.Values
            .Select(accumulator => accumulator.ToRecord())
            .ToArray();
    }

    private sealed class UserAccumulator(
        Guid userId,
        Guid organizationId,
        string email,
        string displayName,
        bool isActive,
        bool isLocked,
        bool mustChangePassword,
        string? passwordHash)
    {
        public Dictionary<Guid, RoleAssignment> Roles { get; } = [];

        public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public AuthenticatedUser ToRecord()
        {
            return new AuthenticatedUser(
                userId,
                organizationId,
                email,
                displayName,
                isActive,
                isLocked,
                mustChangePassword,
                passwordHash,
                Roles.Values.OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
                Permissions.OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }
}
