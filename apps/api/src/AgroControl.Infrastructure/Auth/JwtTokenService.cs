using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgroControl.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgroControl.Infrastructure.Auth;

internal sealed class JwtTokenService(
    IOptions<AuthSettings> options,
    IRefreshTokenProtector refreshTokenProtector,
    TimeProvider timeProvider) : ITokenService
{
    private readonly AuthSettings _settings = options.Value;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenPair IssueTokens(AuthenticatedUser user)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var sessionId = Guid.NewGuid();
        var accessTokenExpiresAt = issuedAt.Add(_settings.AccessTokenLifetime);
        var refreshTokenExpiresAt = issuedAt.Add(_settings.RefreshTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AuthClaims.UserId, user.UserId.ToString()),
            new(AuthClaims.OrganizationId, user.OrganizationId.ToString()),
            new(AuthClaims.SessionId, sessionId.ToString()),
            new(AuthClaims.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Code)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256));

        var refreshToken = $"{sessionId:N}.{refreshTokenProtector.GenerateToken()}";

        return new TokenPair(
            _tokenHandler.WriteToken(token),
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt,
            sessionId);
    }

    public IdentityContext? ReadIdentity(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(ReadClaim(principal, AuthClaims.UserId), out var userId) ||
            !Guid.TryParse(ReadClaim(principal, AuthClaims.OrganizationId), out var organizationId))
        {
            return null;
        }

        Guid? sessionId = null;

        if (Guid.TryParse(ReadClaim(principal, AuthClaims.SessionId), out var parsedSessionId))
        {
            sessionId = parsedSessionId;
        }

        var email = ReadClaim(principal, AuthClaims.Email) ??
                    ReadClaim(principal, ClaimTypes.Email) ??
                    string.Empty;

        return new IdentityContext(
            userId,
            organizationId,
            sessionId,
            email,
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public TokenValidationParameters BuildValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSigningKey(),
            ValidateLifetime = true,
            ClockSkew = _settings.ClockSkew
        };
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
    }

    private static string? ReadClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }
}
