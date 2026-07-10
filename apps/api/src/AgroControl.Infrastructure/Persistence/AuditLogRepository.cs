using System.Text.Json;
using AgroControl.Application.Auth;
using AgroControl.Application.Persistence;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class AuditLogRepository(ISqlConnectionFactory connectionFactory) : IAuditLogRepository
{
    public async Task WriteAsync(
        Guid organizationId,
        Guid? actorUserId,
        string entityName,
        string entityId,
        string action,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            insert into app.audit_logs (
                audit_log_id,
                organization_id,
                actor_user_id,
                entity_name,
                entity_id,
                action,
                metadata,
                created_at)
            values (
                @audit_log_id,
                @organization_id,
                @actor_user_id,
                @entity_name,
                @entity_id,
                @action,
                @metadata::jsonb,
                timezone('utc', now()));
            """;

        command.Parameters.AddWithValue("audit_log_id", Guid.NewGuid());
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("actor_user_id", (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("entity_name", entityName);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(metadata));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
