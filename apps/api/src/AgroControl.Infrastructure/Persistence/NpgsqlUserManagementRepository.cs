using AgroControl.Application.Auth;
using AgroControl.Application.Persistence;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlUserManagementRepository(ISqlConnectionFactory connectionFactory) : IUserManagementRepository
{
    public async Task<IReadOnlyList<AuthenticatedUser>> ListUsersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
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
            order by u.display_name asc;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await NpgsqlAuthRepository.ReadUsersAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleAssignment>> ListRolesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select role_id, code, name
            from app.roles
            where organization_id = @organization_id
            order by name asc;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        var roles = new List<RoleAssignment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new RoleAssignment(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        }

        return roles;
    }

    public async Task<Guid> CreateUserAsync(
        Guid organizationId,
        CreateUserCommand command,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid();

        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureRolesBelongToOrganizationAsync(connection, transaction, organizationId, command.RoleIds, cancellationToken);

        await using (var insertUserCommand = connection.CreateCommand())
        {
            insertUserCommand.Transaction = transaction;
            insertUserCommand.CommandText =
                """
                insert into app.users (
                    user_id,
                    organization_id,
                    email,
                    display_name,
                    password_hash,
                    is_active,
                    is_locked,
                    must_change_password)
                values (
                    @user_id,
                    @organization_id,
                    @email,
                    @display_name,
                    @password_hash,
                    true,
                    false,
                    @must_change_password);
                """;
            insertUserCommand.Parameters.AddWithValue("user_id", userId);
            insertUserCommand.Parameters.AddWithValue("organization_id", organizationId);
            insertUserCommand.Parameters.AddWithValue("email", command.Email);
            insertUserCommand.Parameters.AddWithValue("display_name", command.DisplayName);
            insertUserCommand.Parameters.AddWithValue("password_hash", passwordHash);
            insertUserCommand.Parameters.AddWithValue("must_change_password", command.MustChangePassword);

            await insertUserCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await ReplaceRolesAsync(connection, transaction, userId, command.RoleIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return userId;
    }

    public async Task<AuthenticatedUser?> UpdateUserAsync(
        Guid organizationId,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureRolesBelongToOrganizationAsync(connection, transaction, organizationId, command.RoleIds, cancellationToken);

        await using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                update app.users
                set display_name = @display_name,
                    is_active = @is_active,
                    is_locked = @is_locked,
                    must_change_password = @must_change_password
                where organization_id = @organization_id
                  and user_id = @user_id;
                """;
            updateCommand.Parameters.AddWithValue("display_name", command.DisplayName);
            updateCommand.Parameters.AddWithValue("is_active", command.IsActive);
            updateCommand.Parameters.AddWithValue("is_locked", command.IsLocked);
            updateCommand.Parameters.AddWithValue("must_change_password", command.MustChangePassword);
            updateCommand.Parameters.AddWithValue("organization_id", organizationId);
            updateCommand.Parameters.AddWithValue("user_id", userId);

            var affectedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }
        }

        await ReplaceRolesAsync(connection, transaction, userId, command.RoleIds, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await FindUserAsync(organizationId, userId, cancellationToken);
    }

    public async Task<OrganizationSettingsRecord?> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select organization_id, legal_name, trade_name, coalesce(tax_id, ''), time_zone, currency_code
            from app.organization_settings
            where organization_id = @organization_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OrganizationSettingsRecord(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    public async Task<OrganizationSettingsRecord> UpdateOrganizationSettingsAsync(
        OrganizationSettingsRecord settings,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            insert into app.organization_settings (
                organization_id,
                legal_name,
                trade_name,
                tax_id,
                time_zone,
                currency_code,
                updated_at)
            values (
                @organization_id,
                @legal_name,
                @trade_name,
                @tax_id,
                @time_zone,
                @currency_code,
                timezone('utc', now()))
            on conflict (organization_id) do update
            set legal_name = excluded.legal_name,
                trade_name = excluded.trade_name,
                tax_id = excluded.tax_id,
                time_zone = excluded.time_zone,
                currency_code = excluded.currency_code,
                updated_at = timezone('utc', now());
            """;
        command.Parameters.AddWithValue("organization_id", settings.OrganizationId);
        command.Parameters.AddWithValue("legal_name", settings.LegalName);
        command.Parameters.AddWithValue("trade_name", settings.TradeName);
        command.Parameters.AddWithValue("tax_id", settings.TaxId);
        command.Parameters.AddWithValue("time_zone", settings.TimeZone);
        command.Parameters.AddWithValue("currency_code", settings.CurrencyCode);

        await command.ExecuteNonQueryAsync(cancellationToken);

        return (await GetOrganizationSettingsAsync(settings.OrganizationId, cancellationToken))!;
    }

    private async Task<AuthenticatedUser?> FindUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
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
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = await NpgsqlAuthRepository.ReadUsersAsync(reader, cancellationToken);
        return users.FirstOrDefault();
    }

    private static async Task EnsureRolesBelongToOrganizationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            select count(*)
            from app.roles
            where organization_id = @organization_id
              and role_id = any(@role_ids);
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("role_ids", roleIds.ToArray());

        var count = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);

        if (count != roleIds.Count)
        {
            throw new ValidationException("Los roles informados no pertenecen a la organizacion activa.");
        }
    }

    private static async Task ReplaceRolesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "delete from app.user_roles where user_id = @user_id;";
            deleteCommand.Parameters.AddWithValue("user_id", userId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var roleId in roleIds)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                insert into app.user_roles (user_id, role_id)
                values (@user_id, @role_id);
                """;
            insertCommand.Parameters.AddWithValue("user_id", userId);
            insertCommand.Parameters.AddWithValue("role_id", roleId);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
