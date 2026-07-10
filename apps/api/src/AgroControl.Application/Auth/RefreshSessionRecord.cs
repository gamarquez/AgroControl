namespace AgroControl.Application.Auth;

public sealed record RefreshSessionRecord(
    Guid SessionId,
    Guid UserId,
    Guid OrganizationId,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    Guid? ReplacedBySessionId);
