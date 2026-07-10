namespace AgroControl.Application.Auth;

public sealed record RoleAssignment(
    Guid RoleId,
    string Code,
    string Name);
