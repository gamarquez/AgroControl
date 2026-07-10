namespace AgroControl.Contracts.Auth;

public sealed record AuthSessionResponse(
    AuthUserResponse User,
    AuthTokenResponse Tokens);
