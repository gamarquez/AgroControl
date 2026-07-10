namespace AgroControl.Application.Auth;

public sealed record UpdateUserCommand(
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    IReadOnlyList<Guid> RoleIds);
