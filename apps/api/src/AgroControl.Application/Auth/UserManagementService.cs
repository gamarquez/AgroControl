namespace AgroControl.Application.Auth;

internal sealed class UserManagementService(
    IUserManagementRepository userManagementRepository,
    IAuditLogRepository auditLogRepository,
    IPasswordHasher passwordHasher) : IUserManagementService
{
    public Task<IReadOnlyList<AuthenticatedUser>> ListUsersAsync(IdentityContext identity, CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);
        return userManagementRepository.ListUsersAsync(identity.OrganizationId, cancellationToken);
    }

    public Task<IReadOnlyList<RoleAssignment>> ListRolesAsync(IdentityContext identity, CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);
        return userManagementRepository.ListRolesAsync(identity.OrganizationId, cancellationToken);
    }

    public async Task<AuthenticatedUser> CreateUserAsync(
        IdentityContext identity,
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);

        var normalizedCommand = command with
        {
            Email = AuthValidation.NormalizeEmail(command.Email),
            DisplayName = AuthValidation.ValidateDisplayName(command.DisplayName),
            Password = AuthValidation.ValidatePassword(command.Password),
            RoleIds = AuthValidation.ValidateRoleIds(command.RoleIds)
        };

        var userId = await userManagementRepository.CreateUserAsync(
            identity.OrganizationId,
            normalizedCommand,
            passwordHasher.Hash(normalizedCommand.Password),
            cancellationToken);

        var user = await userManagementRepository.UpdateUserAsync(
            identity.OrganizationId,
            userId,
            new UpdateUserCommand(
                normalizedCommand.DisplayName,
                true,
                false,
                normalizedCommand.MustChangePassword,
                normalizedCommand.RoleIds),
            cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("No fue posible recuperar el usuario creado.");
        }

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "users",
            user.UserId.ToString(),
            "user_created",
            new Dictionary<string, object?> { ["email"] = user.Email, ["roles"] = user.Roles.Select(role => role.Code).ToArray() },
            cancellationToken);

        return user;
    }

    public async Task<AuthenticatedUser> UpdateUserAsync(
        IdentityContext identity,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);

        var normalizedCommand = command with
        {
            DisplayName = AuthValidation.ValidateDisplayName(command.DisplayName),
            RoleIds = AuthValidation.ValidateRoleIds(command.RoleIds)
        };

        var user = await userManagementRepository.UpdateUserAsync(
            identity.OrganizationId,
            userId,
            normalizedCommand,
            cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Usuario no encontrado.");
        }

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "users",
            user.UserId.ToString(),
            "user_updated",
            new Dictionary<string, object?>
            {
                ["isActive"] = user.IsActive,
                ["isLocked"] = user.IsLocked,
                ["mustChangePassword"] = user.MustChangePassword,
                ["roles"] = user.Roles.Select(role => role.Code).ToArray()
            },
            cancellationToken);

        return user;
    }

    public async Task<OrganizationSettingsRecord> GetOrganizationSettingsAsync(
        IdentityContext identity,
        CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);

        return await userManagementRepository.GetOrganizationSettingsAsync(
                   identity.OrganizationId,
                   cancellationToken)
               ?? throw new NotFoundException("No se encontró la configuración de la organización.");
    }

    public async Task<OrganizationSettingsRecord> UpdateOrganizationSettingsAsync(
        IdentityContext identity,
        OrganizationSettingsRecord settings,
        CancellationToken cancellationToken)
    {
        EnsureAdministrator(identity);

        var normalizedSettings = settings with
        {
            OrganizationId = identity.OrganizationId,
            LegalName = AuthValidation.ValidateDisplayName(settings.LegalName),
            TradeName = AuthValidation.ValidateDisplayName(settings.TradeName),
            TaxId = settings.TaxId.Trim(),
            TimeZone = settings.TimeZone.Trim(),
            CurrencyCode = settings.CurrencyCode.Trim().ToUpperInvariant()
        };

        var updated = await userManagementRepository.UpdateOrganizationSettingsAsync(
            normalizedSettings,
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "organization_settings",
            identity.OrganizationId.ToString(),
            "organization_settings_updated",
            new Dictionary<string, object?>
            {
                ["legalName"] = updated.LegalName,
                ["tradeName"] = updated.TradeName,
                ["currencyCode"] = updated.CurrencyCode
            },
            cancellationToken);

        return updated;
    }

    private static void EnsureAdministrator(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para administrar este recurso.");
        }
    }
}
