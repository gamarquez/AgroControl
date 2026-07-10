namespace AgroControl.Application.Auth;

public interface IUserManagementService
{
    Task<IReadOnlyList<AuthenticatedUser>> ListUsersAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleAssignment>> ListRolesAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<AuthenticatedUser> CreateUserAsync(
        IdentityContext identity,
        CreateUserCommand command,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser> UpdateUserAsync(
        IdentityContext identity,
        Guid userId,
        UpdateUserCommand command,
        CancellationToken cancellationToken);

    Task<OrganizationSettingsRecord> GetOrganizationSettingsAsync(
        IdentityContext identity,
        CancellationToken cancellationToken);

    Task<OrganizationSettingsRecord> UpdateOrganizationSettingsAsync(
        IdentityContext identity,
        OrganizationSettingsRecord settings,
        CancellationToken cancellationToken);
}
