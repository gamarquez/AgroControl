namespace AgroControl.Contracts.Auth;

public sealed record LogoutRequest(
    string RefreshToken);
