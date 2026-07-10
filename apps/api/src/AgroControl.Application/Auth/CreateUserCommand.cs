namespace AgroControl.Application.Auth;

public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    string Password,
    bool MustChangePassword,
    IReadOnlyList<Guid> RoleIds);
