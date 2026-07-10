namespace AgroControl.Contracts.Auth;

public sealed record LoginRequest(
    string Email,
    string Password);
