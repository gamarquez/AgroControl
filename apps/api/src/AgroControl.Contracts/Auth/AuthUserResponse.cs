namespace AgroControl.Contracts.Auth;

public sealed record AuthUserResponse(
    Guid UserId,
    Guid OrganizationId,
    string Email,
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    IReadOnlyList<AuthRoleResponse> Roles,
    IReadOnlyList<string> Permissions);
