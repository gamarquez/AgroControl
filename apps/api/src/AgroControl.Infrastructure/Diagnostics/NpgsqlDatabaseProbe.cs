using AgroControl.Application.Diagnostics;
using AgroControl.Application.Persistence;
using Microsoft.Extensions.Logging;

namespace AgroControl.Infrastructure.Diagnostics;

internal sealed class NpgsqlDatabaseProbe(
    ISqlConnectionFactory connectionFactory,
    ILogger<NpgsqlDatabaseProbe> logger) : IAgroControlDatabaseProbe
{
    public async Task<DatabaseProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var startedAt = TimeProvider.System.GetTimestamp();

        if (!connectionFactory.IsConfigured)
        {
            return new DatabaseProbeResult(
                false,
                "La cadena de conexión de PostgreSQL/Supabase no está configurada.",
                TimeProvider.System.GetElapsedTime(startedAt));
        }

        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select 1";

            await command.ExecuteScalarAsync(cancellationToken);

            return new DatabaseProbeResult(
                true,
                "La conexión a PostgreSQL/Supabase respondió correctamente.",
                TimeProvider.System.GetElapsedTime(startedAt));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Database readiness probe failed.");

            return new DatabaseProbeResult(
                false,
                "No fue posible validar la conexión a PostgreSQL/Supabase.",
                TimeProvider.System.GetElapsedTime(startedAt));
        }
    }
}
