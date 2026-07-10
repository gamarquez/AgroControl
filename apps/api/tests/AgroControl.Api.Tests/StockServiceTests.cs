using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;
using AgroControl.Application.Stock;

namespace AgroControl.Api.Tests;

public sealed class StockServiceTests
{
    [Fact]
    public async Task UpdateStockPolicyAsync_WithAdministratorRole_UpdatesPolicyAndWritesAudit()
    {
        var repository = new FakeStockRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new StockService(repository, auditRepository);

        var item = await service.UpdateStockPolicyAsync(
            CreateAdministratorIdentity(),
            repository.Item.ProductId,
            new UpdateStockPolicyCommand(repository.Warehouse.WarehouseId, 5, 20, 8),
            CancellationToken.None);

        Assert.Equal(5, item.Policy.MinQuantity);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "stock_policy_updated");
    }

    [Fact]
    public async Task CreateStockMovementAsync_WithSellerRole_ThrowsAuthorizationException()
    {
        var service = new StockService(new FakeStockRepository(), new FakeAuditLogRepository());

        await Assert.ThrowsAsync<AuthorizationException>(() =>
            service.CreateStockMovementAsync(
                new IdentityContext(Guid.NewGuid(), Guid.NewGuid(), null, "seller@demo.local", ["seller"]),
                new CreateStockMovementCommand(Guid.NewGuid(), Guid.NewGuid(), "adjustment_increase", 1, "Ajuste", null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task ListStockAsync_NormalizesPaginationAndSearch()
    {
        var repository = new FakeStockRepository();
        var service = new StockService(repository, new FakeAuditLogRepository());

        _ = await service.ListStockAsync(
            CreateAdministratorIdentity(),
            new StockListQuery("  balanceado  ", repository.Warehouse.WarehouseId, null, null, null, 0, 500),
            CancellationToken.None);

        Assert.NotNull(repository.LastQuery);
        Assert.Equal("balanceado", repository.LastQuery!.Search);
        Assert.Equal(1, repository.LastQuery.Page);
        Assert.Equal(100, repository.LastQuery.PageSize);
    }

    [Fact]
    public async Task RecordPhysicalInventoryCountAsync_WritesAudit()
    {
        var repository = new FakeStockRepository();
        var auditRepository = new FakeAuditLogRepository();
        var service = new StockService(repository, auditRepository);

        var count = await service.RecordPhysicalInventoryCountAsync(
            CreateAdministratorIdentity(),
            new RecordPhysicalInventoryCountCommand(repository.Warehouse.WarehouseId, repository.Item.ProductId, 11, "Conteo general", null),
            CancellationToken.None);

        Assert.Equal(1, count.DifferenceQuantity);
        Assert.Contains(auditRepository.Entries, entry => entry.Action == "physical_inventory_count_recorded");
    }

    private static IdentityContext CreateAdministratorIdentity()
        => new(Guid.NewGuid(), Guid.NewGuid(), null, "admin@demo.local", ["administrator"]);

    private sealed class FakeStockRepository : IStockRepository
    {
        public UnitOfMeasureRecord Unit { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Kilogramo", "kg", "kg", true, true);

        public WarehouseRecord Warehouse { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Deposito principal", "MAIN", true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        public StockItemRecord Item { get; }

        public StockListQuery? LastQuery { get; private set; }

        public FakeStockRepository()
        {
            Item = new StockItemRecord(
                Warehouse,
                Guid.NewGuid(),
                Warehouse.OrganizationId,
                "Balanceado Premium",
                "BAL-001",
                null,
                null,
                true,
                true,
                null,
                null,
                Unit,
                10,
                false,
                DateTimeOffset.UtcNow,
                new StockPolicyRecord(null, null, null, 1, DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(Guid organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<WarehouseRecord>>([Warehouse]);

        public Task<WarehouseRecord> CreateWarehouseAsync(Guid organizationId, CreateWarehouseCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Warehouse with { Name = command.Name, Code = command.Code, IsDefault = command.IsDefault, IsActive = command.IsActive });

        public Task<WarehouseRecord?> UpdateWarehouseAsync(Guid organizationId, Guid warehouseId, UpdateWarehouseCommand command, CancellationToken cancellationToken)
            => Task.FromResult<WarehouseRecord?>(warehouseId == Warehouse.WarehouseId
                ? Warehouse with { Name = command.Name, Code = command.Code, IsDefault = command.IsDefault, IsActive = command.IsActive }
                : null);

        public Task<StockListResult> ListStockAsync(Guid organizationId, StockListQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new StockListResult([Item], query.Page, query.PageSize, 1));
        }

        public Task<StockItemRecord?> GetStockItemAsync(Guid organizationId, Guid warehouseId, Guid productId, CancellationToken cancellationToken)
            => Task.FromResult<StockItemRecord?>(productId == Item.ProductId && warehouseId == Warehouse.WarehouseId ? Item : null);

        public Task<StockMovementListResult> ListStockMovementsAsync(Guid organizationId, Guid warehouseId, Guid productId, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new StockMovementListResult([], page, pageSize, 0));

        public Task<StockItemRecord?> UpdateStockPolicyAsync(Guid organizationId, Guid productId, UpdateStockPolicyCommand command, CancellationToken cancellationToken)
            => Task.FromResult<StockItemRecord?>(productId == Item.ProductId
                ? Item with
                {
                    Policy = Item.Policy with
                    {
                        MinQuantity = command.MinQuantity,
                        MaxQuantity = command.MaxQuantity,
                        ReorderPoint = command.ReorderPoint
                    }
                }
                : null);

        public Task<StockMovementRecord> CreateStockMovementAsync(Guid organizationId, Guid actorUserId, CreateStockMovementCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new StockMovementRecord(
                Guid.NewGuid(),
                Warehouse,
                command.ProductId,
                command.MovementType,
                command.Quantity,
                command.MovementType.Contains("outbound", StringComparison.OrdinalIgnoreCase) ? -command.Quantity : command.Quantity,
                15,
                command.Reason,
                command.ReferenceDocument,
                command.Notes,
                actorUserId,
                DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<StockAlertRecord>> ListStockAlertsAsync(Guid organizationId, Guid? warehouseId, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StockAlertRecord>>([]);

        public Task<PhysicalInventoryCountRecord> RecordPhysicalInventoryCountAsync(Guid organizationId, Guid actorUserId, RecordPhysicalInventoryCountCommand command, CancellationToken cancellationToken)
            => Task.FromResult(new PhysicalInventoryCountRecord(
                Guid.NewGuid(),
                Warehouse,
                command.ProductId,
                10,
                command.CountedQuantity,
                command.CountedQuantity - 10,
                command.Reason,
                command.Notes,
                actorUserId,
                DateTimeOffset.UtcNow));
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<(string EntityName, string Action)> Entries { get; } = [];

        public Task WriteAsync(Guid organizationId, Guid? actorUserId, string entityName, string entityId, string action, IReadOnlyDictionary<string, object?> metadata, CancellationToken cancellationToken)
        {
            Entries.Add((entityName, action));
            return Task.CompletedTask;
        }
    }
}
