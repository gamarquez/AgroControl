using System.Security.Claims;

namespace AgroControl.Application.Auth;

public interface ITokenService
{
    TokenPair IssueTokens(AuthenticatedUser user);

    IdentityContext? ReadIdentity(ClaimsPrincipal principal);
}
