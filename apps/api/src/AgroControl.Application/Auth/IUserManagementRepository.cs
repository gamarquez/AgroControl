namespace AgroControl.Application.Auth;

public interface IUserManagementRepository
{
    Task<IReadOnlyList<AuthenticatedUser>> ListUsersAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleAssignment>> ListRolesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<Guid> CreateUserAsync(
        Guid organizationId,
        CreateUserCommand command,
        string passwordHash,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser?> UpdateUserAsync(
        Guid organizationId,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken);

    Task<OrganizationSettingsRecord?> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<OrganizationSettingsRecord> UpdateOrganizationSettingsAsync(
        OrganizationSettingsRecord settings,
        CancellationToken cancellationToken);
}
