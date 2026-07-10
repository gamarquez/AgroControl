namespace AgroControl.Application.Auth;

public interface IAuditLogRepository
{
    Task WriteAsync(
        Guid organizationId,
        Guid? actorUserId,
        string entityName,
        string entityId,
        string action,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken);
}
