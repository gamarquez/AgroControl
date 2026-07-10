using AgroControl.Application.Auth;

namespace AgroControl.Application.Sales;

public interface ISalesService
{
    Task<PosProductListResult> ListPosProductsAsync(IdentityContext identity, PosProductListQuery query, CancellationToken cancellationToken);

    Task<SaleListResult> ListSalesAsync(IdentityContext identity, SaleListQuery query, CancellationToken cancellationToken);

    Task<SaleRecord> GetSaleAsync(IdentityContext identity, Guid saleId, CancellationToken cancellationToken);

    Task<SaleRecord> CreateCashSaleAsync(IdentityContext identity, CreateCashSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> CreateAccountSaleAsync(IdentityContext identity, CreateAccountSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> CreateCheckoutSaleAsync(IdentityContext identity, CreateCheckoutSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> ReverseCashSaleAsync(IdentityContext identity, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> ReturnSaleAsync(IdentityContext identity, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken);
}
