using AgroControl.Application.Auth;
using AgroControl.Application.Persistence;

namespace AgroControl.Application.Stock;

internal sealed class StockService(
    IStockRepository stockRepository,
    IAuditLogRepository auditLogRepository) : IStockService
{
    public async Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(IdentityContext identity, CancellationToken cancellationToken)
        => await stockRepository.ListWarehousesAsync(identity.OrganizationId, cancellationToken);

    public async Task<WarehouseRecord> CreateWarehouseAsync(IdentityContext identity, CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var warehouse = await stockRepository.CreateWarehouseAsync(
            identity.OrganizationId,
            new CreateWarehouseCommand(
                StockValidation.ValidateWarehouseName(command.Name),
                StockValidation.ValidateWarehouseCode(command.Code),
                command.IsDefault,
                command.IsActive),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "warehouses",
            warehouse.WarehouseId.ToString(),
            "warehouse_created",
            new Dictionary<string, object?>
            {
                ["code"] = warehouse.Code,
                ["isDefault"] = warehouse.IsDefault,
                ["isActive"] = warehouse.IsActive
            },
            cancellationToken);

        return warehouse;
    }

    public async Task<WarehouseRecord> UpdateWarehouseAsync(IdentityContext identity, Guid warehouseId, UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var warehouse = await stockRepository.UpdateWarehouseAsync(
            identity.OrganizationId,
            StockValidation.ValidateRequiredGuid(warehouseId, "deposito"),
            new UpdateWarehouseCommand(
                StockValidation.ValidateWarehouseName(command.Name),
                StockValidation.ValidateWarehouseCode(command.Code),
                command.IsDefault,
                command.IsActive),
            cancellationToken)
            ?? throw new NotFoundException("Deposito no encontrado.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "warehouses",
            warehouse.WarehouseId.ToString(),
            "warehouse_updated",
            new Dictionary<string, object?>
            {
                ["code"] = warehouse.Code,
                ["isDefault"] = warehouse.IsDefault,
                ["isActive"] = warehouse.IsActive
            },
            cancellationToken);

        return warehouse;
    }

    public async Task<StockListResult> ListStockAsync(IdentityContext identity, StockListQuery query, CancellationToken cancellationToken)
    {
        return await stockRepository.ListStockAsync(
            identity.OrganizationId,
            StockValidation.ValidateListQuery(query),
            cancellationToken);
    }

    public async Task<StockItemRecord> GetStockItemAsync(IdentityContext identity, Guid warehouseId, Guid productId, CancellationToken cancellationToken)
    {
        return await stockRepository.GetStockItemAsync(
            identity.OrganizationId,
            StockValidation.ValidateRequiredGuid(warehouseId, "deposito"),
            productId,
            cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado para stock.");
    }

    public async Task<StockMovementListResult> ListStockMovementsAsync(IdentityContext identity, Guid warehouseId, Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        StockValidation.ValidateRequiredGuid(productId, "producto");

        return await stockRepository.ListStockMovementsAsync(
            identity.OrganizationId,
            StockValidation.ValidateRequiredGuid(warehouseId, "deposito"),
            productId,
            StockValidation.ValidatePage(page),
            StockValidation.ValidatePageSize(pageSize),
            cancellationToken);
    }

    public async Task<StockItemRecord> UpdateStockPolicyAsync(IdentityContext identity, Guid productId, UpdateStockPolicyCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var minQuantity = StockValidation.ValidateOptionalQuantity(command.MinQuantity, "stock minimo");
        var maxQuantity = StockValidation.ValidateOptionalQuantity(command.MaxQuantity, "stock maximo");
        var reorderPoint = StockValidation.ValidateOptionalQuantity(command.ReorderPoint, "punto de reposicion");
        StockValidation.ValidateStockPolicy(minQuantity, maxQuantity, reorderPoint);

        var item = await stockRepository.UpdateStockPolicyAsync(
            identity.OrganizationId,
            StockValidation.ValidateRequiredGuid(productId, "producto"),
            new UpdateStockPolicyCommand(
                StockValidation.ValidateRequiredGuid(command.WarehouseId, "deposito"),
                minQuantity,
                maxQuantity,
                reorderPoint),
            cancellationToken)
            ?? throw new NotFoundException("Producto no encontrado para stock.");

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "stock_balances",
            item.ProductId.ToString(),
            "stock_policy_updated",
            new Dictionary<string, object?>
            {
                ["minQuantity"] = item.Policy.MinQuantity,
                ["maxQuantity"] = item.Policy.MaxQuantity,
                ["reorderPoint"] = item.Policy.ReorderPoint
            },
            cancellationToken);

        return item;
    }

    public async Task<StockMovementRecord> CreateStockMovementAsync(IdentityContext identity, CreateStockMovementCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var normalized = new CreateStockMovementCommand(
            StockValidation.ValidateRequiredGuid(command.WarehouseId, "deposito"),
            StockValidation.ValidateRequiredGuid(command.ProductId, "producto"),
            StockValidation.ValidateMovementType(command.MovementType),
            StockValidation.ValidateQuantity(command.Quantity),
            StockValidation.ValidateReason(command.Reason),
            StockValidation.NormalizeOptional(command.ReferenceDocument),
            StockValidation.NormalizeOptional(command.Notes));

        var movement = await stockRepository.CreateStockMovementAsync(
            identity.OrganizationId,
            identity.UserId,
            normalized,
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "stock_movements",
            movement.StockMovementId.ToString(),
            "stock_movement_recorded",
            new Dictionary<string, object?>
            {
                ["productId"] = movement.ProductId,
                ["movementType"] = movement.MovementType,
                ["quantity"] = movement.Quantity,
                ["resultingQuantity"] = movement.ResultingQuantity
            },
            cancellationToken);

        return movement;
    }

    public async Task<IReadOnlyList<StockAlertRecord>> ListStockAlertsAsync(IdentityContext identity, Guid? warehouseId, int limit, CancellationToken cancellationToken)
        => await stockRepository.ListStockAlertsAsync(
            identity.OrganizationId,
            warehouseId is { } value ? StockValidation.ValidateRequiredGuid(value, "deposito") : null,
            StockValidation.ValidateLimit(limit),
            cancellationToken);

    public async Task<PhysicalInventoryCountRecord> RecordPhysicalInventoryCountAsync(IdentityContext identity, RecordPhysicalInventoryCountCommand command, CancellationToken cancellationToken)
    {
        EnsureWriter(identity);

        var result = await stockRepository.RecordPhysicalInventoryCountAsync(
            identity.OrganizationId,
            identity.UserId,
            new RecordPhysicalInventoryCountCommand(
                StockValidation.ValidateRequiredGuid(command.WarehouseId, "deposito"),
                StockValidation.ValidateRequiredGuid(command.ProductId, "producto"),
                StockValidation.ValidateNonNegativeQuantity(command.CountedQuantity, "cantidad contada"),
                StockValidation.ValidateReason(command.Reason),
                StockValidation.NormalizeOptional(command.Notes)),
            cancellationToken);

        await auditLogRepository.WriteAsync(
            identity.OrganizationId,
            identity.UserId,
            "physical_inventory_counts",
            result.PhysicalInventoryCountId.ToString(),
            "physical_inventory_count_recorded",
            new Dictionary<string, object?>
            {
                ["warehouseId"] = result.Warehouse.WarehouseId,
                ["productId"] = result.ProductId,
                ["countedQuantity"] = result.CountedQuantity,
                ["differenceQuantity"] = result.DifferenceQuantity
            },
            cancellationToken);

        return result;
    }

    private static void EnsureWriter(IdentityContext identity)
    {
        if (!identity.Roles.Contains("administrator", StringComparer.OrdinalIgnoreCase) &&
            !identity.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException("No cuenta con permisos para editar stock.");
        }
    }
}
