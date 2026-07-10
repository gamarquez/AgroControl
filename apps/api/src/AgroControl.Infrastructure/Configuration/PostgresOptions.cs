using Microsoft.Extensions.Configuration;

namespace AgroControl.Infrastructure.Configuration;

public sealed class PostgresOptions
{
    public string? ConnectionString { get; init; }

    public static PostgresOptions FromConfiguration(IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? configuration["Postgres:ConnectionString"]
            ?? configuration["POSTGRES_CONNECTION_STRING"]
            ?? configuration["SUPABASE_DB_CONNECTION_STRING"];

        return new PostgresOptions
        {
            ConnectionString = string.IsNullOrWhiteSpace(connectionString)
                ? null
                : connectionString.Trim()
        };
    }
}
