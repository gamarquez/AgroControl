using System.Data.Common;

namespace AgroControl.Application.Persistence;

public interface ISqlConnectionFactory
{
    bool IsConfigured { get; }

    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
