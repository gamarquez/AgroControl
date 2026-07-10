namespace AgroControl.Application.Auth;

public sealed record IdentityContext(
    Guid UserId,
    Guid OrganizationId,
    Guid? SessionId,
    string Email,
    IReadOnlyList<string> Roles);
