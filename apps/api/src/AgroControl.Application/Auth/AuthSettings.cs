namespace AgroControl.Application.Auth;

public sealed class AuthSettings
{
    public string Issuer { get; init; } = "AgroControl.Api";

    public string Audience { get; init; } = "AgroControl.Web";

    public string SigningKey { get; init; } = "development-signing-key-change-me";

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(14);

    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(2);
}
