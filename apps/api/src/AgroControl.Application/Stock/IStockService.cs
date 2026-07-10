using AgroControl.Application.Auth;

namespace AgroControl.Application.Stock;

public interface IStockService
{
    Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(IdentityContext identity, CancellationToken cancellationToken);

    Task<WarehouseRecord> CreateWarehouseAsync(IdentityContext identity, CreateWarehouseCommand command, CancellationToken cancellationToken);

    Task<WarehouseRecord> UpdateWarehouseAsync(IdentityContext identity, Guid warehouseId, UpdateWarehouseCommand command, CancellationToken cancellationToken);

    Task<StockListResult> ListStockAsync(IdentityContext identity, StockListQuery query, CancellationToken cancellationToken);

    Task<StockItemRecord> GetStockItemAsync(IdentityContext identity, Guid warehouseId, Guid productId, CancellationToken cancellationToken);

    Task<StockMovementListResult> ListStockMovementsAsync(IdentityContext identity, Guid warehouseId, Guid productId, int page, int pageSize, CancellationToken cancellationToken);

    Task<StockItemRecord> UpdateStockPolicyAsync(IdentityContext identity, Guid productId, UpdateStockPolicyCommand command, CancellationToken cancellationToken);

    Task<StockMovementRecord> CreateStockMovementAsync(IdentityContext identity, CreateStockMovementCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockAlertRecord>> ListStockAlertsAsync(IdentityContext identity, Guid? warehouseId, int limit, CancellationToken cancellationToken);

    Task<PhysicalInventoryCountRecord> RecordPhysicalInventoryCountAsync(IdentityContext identity, RecordPhysicalInventoryCountCommand command, CancellationToken cancellationToken);
}
