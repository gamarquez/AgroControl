using AgroControl.Application.Auth;
using AgroControl.Application.Cash;
using AgroControl.Application.Catalog;
using AgroControl.Application.Customers;
using AgroControl.Application.Diagnostics;
using AgroControl.Application.Fiscal;
using AgroControl.Application.Sales;
using AgroControl.Application.Stock;
using Microsoft.Extensions.DependencyInjection;

namespace AgroControl.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IApiReadinessService, ApiReadinessService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IStockService, StockService>();
        services.AddSingleton<ICashService, CashService>();
        services.AddSingleton<ICustomerService, CustomerService>();
        services.AddSingleton<ISalesService, SalesService>();
        services.AddSingleton<IFiscalService, FiscalService>();

        return services;
    }
}
