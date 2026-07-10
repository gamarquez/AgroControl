namespace AgroControl.Application.Stock;

public interface IStockRepository
{
    Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<WarehouseRecord> CreateWarehouseAsync(Guid organizationId, CreateWarehouseCommand command, CancellationToken cancellationToken);

    Task<WarehouseRecord?> UpdateWarehouseAsync(Guid organizationId, Guid warehouseId, UpdateWarehouseCommand command, CancellationToken cancellationToken);

    Task<StockListResult> ListStockAsync(Guid organizationId, StockListQuery query, CancellationToken cancellationToken);

    Task<StockItemRecord?> GetStockItemAsync(Guid organizationId, Guid warehouseId, Guid productId, CancellationToken cancellationToken);

    Task<StockMovementListResult> ListStockMovementsAsync(Guid organizationId, Guid warehouseId, Guid productId, int page, int pageSize, CancellationToken cancellationToken);

    Task<StockItemRecord?> UpdateStockPolicyAsync(Guid organizationId, Guid productId, UpdateStockPolicyCommand command, CancellationToken cancellationToken);

    Task<StockMovementRecord> CreateStockMovementAsync(Guid organizationId, Guid actorUserId, CreateStockMovementCommand command, CancellationToken cancellationToken);

    Task<IReadOnlyList<StockAlertRecord>> ListStockAlertsAsync(Guid organizationId, Guid? warehouseId, int limit, CancellationToken cancellationToken);

    Task<PhysicalInventoryCountRecord> RecordPhysicalInventoryCountAsync(Guid organizationId, Guid actorUserId, RecordPhysicalInventoryCountCommand command, CancellationToken cancellationToken);
}
