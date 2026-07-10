namespace AgroControl.Application.Sales;

public interface ISalesRepository
{
    Task<PosProductListResult> ListPosProductsAsync(Guid organizationId, PosProductListQuery query, CancellationToken cancellationToken);

    Task<SaleListResult> ListSalesAsync(Guid organizationId, SaleListQuery query, CancellationToken cancellationToken);

    Task<SaleRecord?> GetSaleAsync(Guid organizationId, Guid saleId, CancellationToken cancellationToken);

    Task<SaleRecord> CreateCashSaleAsync(Guid organizationId, Guid actorUserId, CreateCashSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> CreateAccountSaleAsync(Guid organizationId, Guid actorUserId, CreateAccountSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> CreateCheckoutSaleAsync(Guid organizationId, Guid actorUserId, CreateCheckoutSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> ReverseCashSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken);

    Task<SaleRecord> ReturnSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken);
}
