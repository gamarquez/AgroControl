using AgroControl.Contracts.Auth;

namespace AgroControl.Contracts.Users;

public sealed record RoleListResponse(
    IReadOnlyList<AuthRoleResponse> Items);
