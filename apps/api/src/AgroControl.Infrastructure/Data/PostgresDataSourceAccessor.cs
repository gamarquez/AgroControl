using AgroControl.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AgroControl.Infrastructure.Data;

internal sealed class PostgresDataSourceAccessor(IOptions<PostgresOptions> options) : IDisposable
{
    private readonly Lazy<NpgsqlDataSource?> _dataSource = new(() =>
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
        {
            return null;
        }

        var builder = new NpgsqlDataSourceBuilder(options.Value.ConnectionString);
        return builder.Build();
    });

    public bool IsConfigured => _dataSource.Value is not null;

    public NpgsqlDataSource? DataSource => _dataSource.Value;

    public void Dispose()
    {
        _dataSource.Value?.Dispose();
    }
}
