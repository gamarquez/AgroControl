using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;
using AgroControl.Application.Stock;
using AgroControl.Contracts.Catalog;
using AgroControl.Contracts.Stock;
using System.Security.Claims;

namespace AgroControl.Api.Infrastructure;

internal static class StockApiEndpoints
{
    public static IEndpointRouteBuilder MapStockApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/stock").RequireAuthorization();

        api.MapGet(
                "/warehouses",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await stockService.ListWarehousesAsync(identity, cancellationToken);
                    return TypedResults.Ok(new WarehouseListResponse(result.Select(ToWarehouseResponse).ToArray()));
                })
            .WithName("ListStockWarehouses");

        api.MapPost(
                "/warehouses",
                async (
                    CreateWarehouseRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var warehouse = await stockService.CreateWarehouseAsync(
                        identity,
                        new CreateWarehouseCommand(request.Name, request.Code, request.IsDefault, request.IsActive),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/stock/warehouses/{warehouse.WarehouseId}", ToWarehouseResponse(warehouse));
                })
            .WithName("CreateStockWarehouse");

        api.MapPatch(
                "/warehouses/{warehouseId:guid}",
                async (
                    Guid warehouseId,
                    UpdateWarehouseRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var warehouse = await stockService.UpdateWarehouseAsync(
                        identity,
                        warehouseId,
                        new UpdateWarehouseCommand(request.Name, request.Code, request.IsDefault, request.IsActive),
                        cancellationToken);

                    return TypedResults.Ok(ToWarehouseResponse(warehouse));
                })
            .WithName("UpdateStockWarehouse");

        api.MapGet(
                "/alerts",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    Guid? warehouseId,
                    int? limit,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await stockService.ListStockAlertsAsync(identity, warehouseId, limit ?? 10, cancellationToken);
                    return TypedResults.Ok(new StockAlertListResponse(result.Select(ToStockAlertResponse).ToArray()));
                })
            .WithName("ListStockAlerts");

        api.MapGet(
                "/products",
                async (
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    string? search,
                    Guid? warehouseId,
                    Guid? categoryId,
                    Guid? brandId,
                    bool? isLowStock,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await stockService.ListStockAsync(
                        identity,
                        new StockListQuery(search, warehouseId, categoryId, brandId, isLowStock, page ?? 1, pageSize ?? 20),
                        cancellationToken);

                    return TypedResults.Ok(new StockListResponse(
                        result.Items.Select(ToStockSummaryResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListStockProducts");

        api.MapGet(
                "/products/{productId:guid}",
                async (
                    Guid productId,
                    Guid warehouseId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var item = await stockService.GetStockItemAsync(identity, warehouseId, productId, cancellationToken);
                    return TypedResults.Ok(ToStockDetailResponse(item));
                })
            .WithName("GetStockProduct");

        api.MapGet(
                "/products/{productId:guid}/movements",
                async (
                    Guid productId,
                    Guid warehouseId,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    int? page,
                    int? pageSize,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var result = await stockService.ListStockMovementsAsync(
                        identity,
                        warehouseId,
                        productId,
                        page ?? 1,
                        pageSize ?? 20,
                        cancellationToken);

                    return TypedResults.Ok(new StockMovementListResponse(
                        result.Items.Select(ToStockMovementResponse).ToArray(),
                        result.Page,
                        result.PageSize,
                        result.Total));
                })
            .WithName("ListStockMovements");

        api.MapPut(
                "/products/{productId:guid}/policy",
                async (
                    Guid productId,
                    UpdateStockPolicyRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var item = await stockService.UpdateStockPolicyAsync(
                        identity,
                        productId,
                        new UpdateStockPolicyCommand(
                            request.WarehouseId,
                            request.MinQuantity,
                            request.MaxQuantity,
                            request.ReorderPoint),
                        cancellationToken);

                    return TypedResults.Ok(ToStockDetailResponse(item));
                })
            .WithName("UpdateStockPolicy");

        api.MapPost(
                "/movements",
                async (
                    CreateStockMovementRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var movement = await stockService.CreateStockMovementAsync(
                        identity,
                        new CreateStockMovementCommand(
                            request.WarehouseId,
                            request.ProductId,
                            request.MovementType,
                            request.Quantity,
                            request.Reason,
                            request.ReferenceDocument,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/stock/movements/{movement.StockMovementId}", ToStockMovementResponse(movement));
                })
            .WithName("CreateStockMovement");

        api.MapPost(
                "/physical-counts",
                async (
                    RecordPhysicalInventoryCountRequest request,
                    ClaimsPrincipal principal,
                    ITokenService tokenService,
                    IStockService stockService,
                    CancellationToken cancellationToken) =>
                {
                    var identity = principal.GetRequiredIdentity(tokenService);
                    var count = await stockService.RecordPhysicalInventoryCountAsync(
                        identity,
                        new RecordPhysicalInventoryCountCommand(
                            request.WarehouseId,
                            request.ProductId,
                            request.CountedQuantity,
                            request.Reason,
                            request.Notes),
                        cancellationToken);

                    return TypedResults.Created($"/api/v1/stock/physical-counts/{count.PhysicalInventoryCountId}", ToPhysicalCountResponse(count));
                })
            .WithName("RecordPhysicalInventoryCount");

        return app;
    }

    private static WarehouseResponse ToWarehouseResponse(WarehouseRecord warehouse)
        => new(
            warehouse.WarehouseId,
            warehouse.Name,
            warehouse.Code,
            warehouse.IsDefault,
            warehouse.IsActive,
            warehouse.CreatedAt,
            warehouse.UpdatedAt);

    private static StockSummaryResponse ToStockSummaryResponse(StockItemRecord item)
        => new(
            ToWarehouseResponse(item.Warehouse),
            item.ProductId,
            item.Name,
            item.InternalCode,
            item.Sku,
            item.Barcode,
            item.IsActive,
            item.AllowsFraction,
            item.Category is null ? null : ToCategoryResponse(item.Category),
            item.Brand is null ? null : ToBrandResponse(item.Brand),
            ToUnitResponse(item.BaseUnit),
            item.OnHandQuantity,
            item.IsLowStock,
            item.LastMovementAt,
            ToPolicyResponse(item.Policy));

    private static StockDetailResponse ToStockDetailResponse(StockItemRecord item)
        => new(
            ToWarehouseResponse(item.Warehouse),
            item.ProductId,
            item.Name,
            item.InternalCode,
            item.Sku,
            item.Barcode,
            item.IsActive,
            item.AllowsFraction,
            item.Category is null ? null : ToCategoryResponse(item.Category),
            item.Brand is null ? null : ToBrandResponse(item.Brand),
            ToUnitResponse(item.BaseUnit),
            item.OnHandQuantity,
            item.IsLowStock,
            item.LastMovementAt,
            ToPolicyResponse(item.Policy));

    private static StockMovementResponse ToStockMovementResponse(StockMovementRecord movement)
        => new(
            movement.StockMovementId,
            ToWarehouseResponse(movement.Warehouse),
            movement.ProductId,
            movement.MovementType,
            movement.Quantity,
            movement.QuantityDelta,
            movement.ResultingQuantity,
            movement.Reason,
            movement.ReferenceDocument,
            movement.Notes,
            movement.PerformedByUserId,
            movement.CreatedAt);

    private static StockAlertResponse ToStockAlertResponse(StockAlertRecord alert)
        => new(
            ToWarehouseResponse(alert.Warehouse),
            alert.ProductId,
            alert.ProductName,
            alert.InternalCode,
            alert.UnitSymbol,
            alert.OnHandQuantity,
            alert.ReorderPoint,
            alert.MinQuantity,
            alert.LastMovementAt);

    private static PhysicalInventoryCountResponse ToPhysicalCountResponse(PhysicalInventoryCountRecord count)
        => new(
            count.PhysicalInventoryCountId,
            ToWarehouseResponse(count.Warehouse),
            count.ProductId,
            count.ExpectedQuantity,
            count.CountedQuantity,
            count.DifferenceQuantity,
            count.Reason,
            count.Notes,
            count.PerformedByUserId,
            count.CreatedAt);

    private static StockPolicyResponse ToPolicyResponse(StockPolicyRecord policy)
        => new(
            policy.MinQuantity,
            policy.MaxQuantity,
            policy.ReorderPoint,
            policy.VersionNumber,
            policy.UpdatedAt);

    private static ProductCategoryResponse ToCategoryResponse(ProductCategoryRecord category)
        => new(category.CategoryId, category.Name, category.Description, category.IsActive);

    private static BrandResponse ToBrandResponse(BrandRecord brand)
        => new(brand.BrandId, brand.Name, brand.Description, brand.IsActive);

    private static UnitOfMeasureResponse ToUnitResponse(UnitOfMeasureRecord unit)
        => new(unit.UnitId, unit.Name, unit.Code, unit.Symbol, unit.AllowsFraction, unit.IsActive);
}
