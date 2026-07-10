using AgroControl.Application.Auth;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class ClaimsPrincipalExtensions
{
    public static IdentityContext GetRequiredIdentity(this ClaimsPrincipal principal, ITokenService tokenService)
    {
        return tokenService.ReadIdentity(principal)
               ?? throw new AuthenticationException("No se pudo resolver la identidad actual.");
    }
}
