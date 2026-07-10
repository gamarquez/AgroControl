using AgroControl.Application.Persistence;
using System.Data.Common;

namespace AgroControl.Infrastructure.Data;

internal sealed class NpgsqlConnectionFactory(PostgresDataSourceAccessor accessor) : ISqlConnectionFactory
{
    public bool IsConfigured => accessor.IsConfigured;

    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var dataSource = accessor.DataSource;

        if (dataSource is null)
        {
            throw new InvalidOperationException(
                "No PostgreSQL connection string was configured. Set POSTGRES_CONNECTION_STRING or SUPABASE_DB_CONNECTION_STRING.");
        }

        return await dataSource.OpenConnectionAsync(cancellationToken);
    }
}
