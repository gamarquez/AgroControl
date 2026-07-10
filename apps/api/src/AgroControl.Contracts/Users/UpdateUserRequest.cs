namespace AgroControl.Contracts.Users;

public sealed record UpdateUserRequest(
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    bool MustChangePassword,
    IReadOnlyList<Guid> RoleIds);
