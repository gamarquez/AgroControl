namespace AgroControl.Application.Auth;

public sealed record AuthResult(
    AuthenticatedUser User,
    TokenPair Tokens);
