using AgroControl.Application.Auth;
using AgroControl.Application.Cash;
using AgroControl.Application.Catalog;
using AgroControl.Application.Customers;
using AgroControl.Application.Diagnostics;
using AgroControl.Application.Fiscal;
using AgroControl.Application.Persistence;
using AgroControl.Application.Sales;
using AgroControl.Application.Stock;
using AgroControl.Infrastructure.Auth;
using AgroControl.Infrastructure.Configuration;
using AgroControl.Infrastructure.Data;
using AgroControl.Infrastructure.Diagnostics;
using AgroControl.Infrastructure.Fiscal;
using AgroControl.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgroControl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        var postgresOptions = PostgresOptions.FromConfiguration(configuration);
        var authSettings = DeploymentConfiguration.CreateAuthSettings(configuration, isProduction);
        var arcaOptions = ArcaOptions.FromConfiguration(configuration);

        services.AddSingleton(Options.Create(postgresOptions));
        services.AddSingleton(Options.Create(authSettings));
        services.AddSingleton(Options.Create(arcaOptions));
        services.AddSingleton<PostgresDataSourceAccessor>();
        services.AddSingleton<ISqlConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IAgroControlDatabaseProbe, NpgsqlDatabaseProbe>();
        services.AddSingleton<Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordHasher>(serviceProvider => serviceProvider.GetRequiredService<Pbkdf2PasswordHasher>());
        services.AddSingleton<IRefreshTokenProtector, Sha256RefreshTokenProtector>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<ITokenService>(serviceProvider => serviceProvider.GetRequiredService<JwtTokenService>());
        services.AddSingleton<IAuthRepository, NpgsqlAuthRepository>();
        services.AddSingleton<IUserManagementRepository, NpgsqlUserManagementRepository>();
        services.AddSingleton<ICatalogRepository, NpgsqlCatalogRepository>();
        services.AddSingleton<IStockRepository, NpgsqlStockRepository>();
        services.AddSingleton<ICashRepository, NpgsqlCashRepository>();
        services.AddSingleton<ICustomerRepository, NpgsqlCustomerRepository>();
        services.AddSingleton<ISalesRepository, NpgsqlSalesRepository>();
        services.AddSingleton<IFiscalRepository, NpgsqlFiscalRepository>();
        services.AddSingleton<IArcaFiscalService, ArcaFiscalService>();
        services.AddSingleton<IAuditLogRepository, AuditLogRepository>();

        return services;
    }
}
