namespace AgroControl.Contracts.Auth;

public sealed record AuthRoleResponse(
    Guid RoleId,
    string Code,
    string Name);
