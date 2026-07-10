using AgroControl.Contracts.Auth;

namespace AgroControl.Contracts.Users;

public sealed record UserListResponse(
    IReadOnlyList<AuthUserResponse> Items);
