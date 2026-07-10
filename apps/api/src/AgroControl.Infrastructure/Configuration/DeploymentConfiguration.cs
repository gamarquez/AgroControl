using AgroControl.Application.Auth;
using Microsoft.Extensions.Configuration;

namespace AgroControl.Infrastructure.Configuration;

public static class DeploymentConfiguration
{
    private static readonly string[] DevelopmentOrigins =
    [
        "http://127.0.0.1:3000",
        "http://127.0.0.1:3001",
        "http://127.0.0.1:3100",
        "http://localhost:3000",
        "http://localhost:3001",
        "http://localhost:3100"
    ];

    public static AuthSettings CreateAuthSettings(IConfiguration configuration, bool isProduction)
    {
        var configuredSigningKey = configuration["Auth:SigningKey"];
        var signingKey = string.IsNullOrWhiteSpace(configuredSigningKey)
            ? "development-signing-key-change-me"
            : configuredSigningKey.Trim();

        var settings = new AuthSettings
        {
            Issuer = configuration["Auth:Issuer"] ?? "AgroControl.Api",
            Audience = configuration["Auth:Audience"] ?? "AgroControl.Web",
            SigningKey = signingKey,
            AccessTokenLifetime = TimeSpan.FromMinutes(configuration.GetValue("Auth:AccessTokenLifetimeMinutes", 15)),
            RefreshTokenLifetime = TimeSpan.FromDays(configuration.GetValue("Auth:RefreshTokenLifetimeDays", 14)),
            ClockSkew = TimeSpan.FromSeconds(configuration.GetValue("Auth:ClockSkewSeconds", 120))
        };

        if (isProduction)
        {
            if (string.IsNullOrWhiteSpace(configuredSigningKey) ||
                string.Equals(signingKey, "development-signing-key-change-me", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("En produccion se requiere Auth__SigningKey con un valor seguro.");
            }

            if (string.IsNullOrWhiteSpace(settings.Issuer) || !Uri.TryCreate(settings.Issuer, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException("En produccion se requiere Auth__Issuer con una URL absoluta.");
            }

            if (string.IsNullOrWhiteSpace(settings.Audience) || !Uri.TryCreate(settings.Audience, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException("En produccion se requiere Auth__Audience con una URL absoluta.");
            }

            var allowedHosts = configuration["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(allowedHosts) || string.Equals(allowedHosts.Trim(), "*", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("En produccion se requiere AllowedHosts con hosts explicitos.");
            }
        }

        return settings;
    }

    public static string[] GetAllowedCorsOrigins(IConfiguration configuration, bool isProduction)
    {
        var configuredOrigins = configuration["Cors:AllowedOrigins"] ?? configuration["CORS_ALLOWED_ORIGINS"];
        var origins = configuredOrigins?
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins is { Length: > 0 })
        {
            return origins;
        }

        if (isProduction)
        {
            throw new InvalidOperationException("En produccion se requiere Cors__AllowedOrigins o CORS_ALLOWED_ORIGINS.");
        }

        return DevelopmentOrigins;
    }
}
