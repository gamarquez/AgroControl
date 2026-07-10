namespace AgroControl.Application.Auth;

public sealed record AuthenticatedUser(
    Guid UserId,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    string? PasswordHash,
    IReadOnlyList<RoleAssignment> Roles,
    IReadOnlyList<string> Permissions);
