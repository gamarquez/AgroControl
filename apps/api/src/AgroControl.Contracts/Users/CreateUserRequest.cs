namespace AgroControl.Contracts.Users;

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    bool MustChangePassword,
    IReadOnlyList<Guid> RoleIds);
