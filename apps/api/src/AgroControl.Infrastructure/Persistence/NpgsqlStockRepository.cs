using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;
using AgroControl.Application.Persistence;
using AgroControl.Application.Stock;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlStockRepository(ISqlConnectionFactory connectionFactory) : IStockRepository
{
    public async Task<IReadOnlyList<WarehouseRecord>> ListWarehousesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at
            from app.warehouses w
            where w.organization_id = @organization_id
            order by w.is_default desc, w.name asc;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<WarehouseRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadWarehouse(reader, 0));
        }

        return items;
    }

    public async Task<WarehouseRecord> CreateWarehouseAsync(Guid organizationId, CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.upsert_warehouse(
                @organization_id,
                null,
                @name,
                @code,
                @is_default,
                @is_active);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        dbCommand.Parameters.AddWithValue("code", command.Code);
        dbCommand.Parameters.AddWithValue("is_default", command.IsDefault);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            var warehouseId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                     ?? throw new InvalidOperationException("No fue posible crear el deposito."));
            return await GetWarehouseAsync(organizationId, warehouseId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar el deposito creado.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<WarehouseRecord?> UpdateWarehouseAsync(Guid organizationId, Guid warehouseId, UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.upsert_warehouse(
                @organization_id,
                @warehouse_id,
                @name,
                @code,
                @is_default,
                @is_active);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("warehouse_id", warehouseId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        dbCommand.Parameters.AddWithValue("code", command.Code);
        dbCommand.Parameters.AddWithValue("is_default", command.IsDefault);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            _ = await dbCommand.ExecuteScalarAsync(cancellationToken);
            return await GetWarehouseAsync(organizationId, warehouseId, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<StockListResult> ListStockAsync(Guid organizationId, StockListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with selected_warehouse as (
                select w.warehouse_id
                from app.warehouses w
                where w.organization_id = @organization_id
                  and (
                        (@warehouse_id is not null and w.warehouse_id = @warehouse_id)
                        or (@warehouse_id is null and w.is_default = true and w.is_active = true)
                  )
                order by w.is_default desc, w.created_at asc
                limit 1
            ),
            filtered_stock as (
                select
                    sw.warehouse_id,
                    w.organization_id,
                    w.name as warehouse_name,
                    w.code as warehouse_code,
                    w.is_default as warehouse_is_default,
                    w.is_active as warehouse_is_active,
                    w.created_at as warehouse_created_at,
                    w.updated_at as warehouse_updated_at,
                    p.product_id,
                    p.organization_id as product_organization_id,
                    p.name,
                    p.internal_code,
                    p.sku,
                    p.barcode,
                    p.is_active,
                    p.allows_fraction,
                    c.category_id,
                    c.name as category_name,
                    c.description as category_description,
                    c.is_active as category_is_active,
                    b.brand_id,
                    b.name as brand_name,
                    b.description as brand_description,
                    b.is_active as brand_is_active,
                    u.unit_id,
                    u.name as unit_name,
                    u.code as unit_code,
                    u.symbol as unit_symbol,
                    u.allows_fraction as unit_allows_fraction,
                    u.is_active as unit_is_active,
                    coalesce(sb.on_hand_quantity, 0) as on_hand_quantity,
                    sb.min_quantity,
                    sb.max_quantity,
                    sb.reorder_point,
                    coalesce(sb.version_number, 1) as version_number,
                    coalesce(sb.updated_at, p.updated_at) as stock_updated_at,
                    lm.last_movement_at,
                    case
                        when coalesce(sb.reorder_point, sb.min_quantity) is not null
                            and coalesce(sb.on_hand_quantity, 0) <= coalesce(sb.reorder_point, sb.min_quantity)
                        then true
                        else false
                    end as is_low_stock,
                    count(*) over() as total_count
                from selected_warehouse sw
                join app.warehouses w on w.warehouse_id = sw.warehouse_id
                join app.products p on p.organization_id = @organization_id
                left join app.product_categories c on c.category_id = p.category_id
                left join app.brands b on b.brand_id = p.brand_id
                join app.units_of_measure u on u.unit_id = p.base_unit_id
                left join app.stock_balances sb
                    on sb.organization_id = p.organization_id
                   and sb.warehouse_id = sw.warehouse_id
                   and sb.product_id = p.product_id
                left join lateral (
                    select max(sm.created_at) as last_movement_at
                    from app.stock_movements sm
                    where sm.organization_id = p.organization_id
                      and sm.warehouse_id = sw.warehouse_id
                      and sm.product_id = p.product_id
                ) lm on true
                where (@search is null
                       or p.name ilike '%' || @search || '%'
                       or p.internal_code ilike '%' || @search || '%'
                       or coalesce(p.sku, '') ilike '%' || @search || '%'
                       or coalesce(p.barcode, '') ilike '%' || @search || '%')
                  and (@category_id is null or p.category_id = @category_id)
                  and (@brand_id is null or p.brand_id = @brand_id)
                  and (
                        @is_low_stock is null
                        or (
                            case
                                when coalesce(sb.reorder_point, sb.min_quantity) is not null
                                    and coalesce(sb.on_hand_quantity, 0) <= coalesce(sb.reorder_point, sb.min_quantity)
                                then true
                                else false
                            end
                        ) = @is_low_stock
                  )
            )
            select *
            from filtered_stock
            order by is_low_stock desc, name asc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "warehouse_id", query.WarehouseId);
        AddNullable(command, "search", query.Search);
        AddNullable(command, "category_id", query.CategoryId);
        AddNullable(command, "brand_id", query.BrandId);
        AddNullable(command, "is_low_stock", query.IsLowStock);
        command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<StockItemRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadStockItem(reader));
            total = reader.GetInt32(38);
        }

        return new StockListResult(items, query.Page, query.PageSize, total);
    }

    public async Task<StockItemRecord?> GetStockItemAsync(Guid organizationId, Guid warehouseId, Guid productId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                w.warehouse_id,
                w.organization_id,
                w.name as warehouse_name,
                w.code as warehouse_code,
                w.is_default as warehouse_is_default,
                w.is_active as warehouse_is_active,
                w.created_at as warehouse_created_at,
                w.updated_at as warehouse_updated_at,
                p.product_id,
                p.organization_id as product_organization_id,
                p.name,
                p.internal_code,
                p.sku,
                p.barcode,
                p.is_active,
                p.allows_fraction,
                c.category_id,
                c.name as category_name,
                c.description as category_description,
                c.is_active as category_is_active,
                b.brand_id,
                b.name as brand_name,
                b.description as brand_description,
                b.is_active as brand_is_active,
                u.unit_id,
                u.name as unit_name,
                u.code as unit_code,
                u.symbol as unit_symbol,
                u.allows_fraction as unit_allows_fraction,
                u.is_active as unit_is_active,
                coalesce(sb.on_hand_quantity, 0) as on_hand_quantity,
                sb.min_quantity,
                sb.max_quantity,
                sb.reorder_point,
                coalesce(sb.version_number, 1) as version_number,
                coalesce(sb.updated_at, p.updated_at) as stock_updated_at,
                lm.last_movement_at,
                case
                    when coalesce(sb.reorder_point, sb.min_quantity) is not null
                        and coalesce(sb.on_hand_quantity, 0) <= coalesce(sb.reorder_point, sb.min_quantity)
                    then true
                    else false
                end as is_low_stock
            from app.warehouses w
            join app.products p on p.organization_id = w.organization_id
            left join app.product_categories c on c.category_id = p.category_id
            left join app.brands b on b.brand_id = p.brand_id
            join app.units_of_measure u on u.unit_id = p.base_unit_id
            left join app.stock_balances sb
                on sb.organization_id = p.organization_id
               and sb.warehouse_id = w.warehouse_id
               and sb.product_id = p.product_id
            left join lateral (
                select max(sm.created_at) as last_movement_at
                from app.stock_movements sm
                where sm.organization_id = p.organization_id
                  and sm.warehouse_id = w.warehouse_id
                  and sm.product_id = p.product_id
            ) lm on true
            where w.organization_id = @organization_id
              and w.warehouse_id = @warehouse_id
              and p.product_id = @product_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("warehouse_id", warehouseId);
        command.Parameters.AddWithValue("product_id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadStockItem(reader)
            : null;
    }

    public async Task<StockMovementListResult> ListStockMovementsAsync(Guid organizationId, Guid warehouseId, Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                sm.stock_movement_id,
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at,
                sm.product_id,
                sm.movement_type,
                sm.quantity,
                sm.quantity_delta,
                sm.resulting_quantity,
                sm.reason,
                sm.reference_document,
                sm.notes,
                sm.performed_by_user_id,
                sm.created_at,
                count(*) over() as total_count
            from app.stock_movements sm
            join app.warehouses w on w.warehouse_id = sm.warehouse_id
            where sm.organization_id = @organization_id
              and sm.warehouse_id = @warehouse_id
              and sm.product_id = @product_id
            order by sm.created_at desc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("warehouse_id", warehouseId);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("page_size", pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<StockMovementRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadStockMovement(reader));
            total = reader.GetInt32(19);
        }

        return new StockMovementListResult(items, page, pageSize, total);
    }

    public async Task<StockItemRecord?> UpdateStockPolicyAsync(Guid organizationId, Guid productId, UpdateStockPolicyCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.update_stock_policy(
                @organization_id,
                @product_id,
                @warehouse_id,
                @min_quantity,
                @max_quantity,
                @reorder_point);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("product_id", productId);
        dbCommand.Parameters.AddWithValue("warehouse_id", command.WarehouseId);
        AddNullable(dbCommand, "min_quantity", command.MinQuantity);
        AddNullable(dbCommand, "max_quantity", command.MaxQuantity);
        AddNullable(dbCommand, "reorder_point", command.ReorderPoint);

        try
        {
            _ = await dbCommand.ExecuteScalarAsync(cancellationToken);
            return await GetStockItemAsync(organizationId, command.WarehouseId, productId, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<StockMovementRecord> CreateStockMovementAsync(Guid organizationId, Guid actorUserId, CreateStockMovementCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.record_stock_movement(
                @organization_id,
                @product_id,
                @warehouse_id,
                @movement_type,
                @quantity,
                @reason,
                @reference_document,
                @notes,
                @performed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("product_id", command.ProductId);
        dbCommand.Parameters.AddWithValue("warehouse_id", command.WarehouseId);
        dbCommand.Parameters.AddWithValue("movement_type", command.MovementType);
        dbCommand.Parameters.AddWithValue("quantity", command.Quantity);
        dbCommand.Parameters.AddWithValue("reason", command.Reason);
        AddNullable(dbCommand, "reference_document", command.ReferenceDocument);
        AddNullable(dbCommand, "notes", command.Notes);
        dbCommand.Parameters.AddWithValue("performed_by_user_id", actorUserId);

        try
        {
            var movementId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                     ?? throw new InvalidOperationException("No fue posible registrar el movimiento de stock."));

            return await GetStockMovementAsync(organizationId, movementId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar el movimiento de stock creado.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<IReadOnlyList<StockAlertRecord>> ListStockAlertsAsync(Guid organizationId, Guid? warehouseId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with selected_warehouse as (
                select w.warehouse_id
                from app.warehouses w
                where w.organization_id = @organization_id
                  and (
                        (@warehouse_id is not null and w.warehouse_id = @warehouse_id)
                        or (@warehouse_id is null and w.is_default = true and w.is_active = true)
                  )
                order by w.is_default desc, w.created_at asc
                limit 1
            )
            select
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at,
                p.product_id,
                p.name,
                p.internal_code,
                u.symbol,
                coalesce(sb.on_hand_quantity, 0) as on_hand_quantity,
                sb.reorder_point,
                sb.min_quantity,
                lm.last_movement_at
            from selected_warehouse sw
            join app.warehouses w on w.warehouse_id = sw.warehouse_id
            join app.products p on p.organization_id = @organization_id and p.is_active = true
            join app.units_of_measure u on u.unit_id = p.base_unit_id
            left join app.stock_balances sb
                on sb.organization_id = p.organization_id
               and sb.warehouse_id = sw.warehouse_id
               and sb.product_id = p.product_id
            left join lateral (
                select max(sm.created_at) as last_movement_at
                from app.stock_movements sm
                where sm.organization_id = p.organization_id
                  and sm.warehouse_id = sw.warehouse_id
                  and sm.product_id = p.product_id
            ) lm on true
            where coalesce(sb.reorder_point, sb.min_quantity) is not null
              and coalesce(sb.on_hand_quantity, 0) <= coalesce(sb.reorder_point, sb.min_quantity)
            order by coalesce(sb.on_hand_quantity, 0) asc, p.name asc
            fetch first @limit rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "warehouse_id", warehouseId);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<StockAlertRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var warehouse = ReadWarehouse(reader, 0);
            items.Add(new StockAlertRecord(
                warehouse,
                reader.GetGuid(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetDecimal(12),
                reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                reader.IsDBNull(14) ? null : reader.GetDecimal(14),
                reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15)));
        }

        return items;
    }

    public async Task<PhysicalInventoryCountRecord> RecordPhysicalInventoryCountAsync(Guid organizationId, Guid actorUserId, RecordPhysicalInventoryCountCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.record_physical_inventory_count(
                @organization_id,
                @warehouse_id,
                @product_id,
                @counted_quantity,
                @reason,
                @notes,
                @performed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("warehouse_id", command.WarehouseId);
        dbCommand.Parameters.AddWithValue("product_id", command.ProductId);
        dbCommand.Parameters.AddWithValue("counted_quantity", command.CountedQuantity);
        dbCommand.Parameters.AddWithValue("reason", command.Reason);
        AddNullable(dbCommand, "notes", command.Notes);
        dbCommand.Parameters.AddWithValue("performed_by_user_id", actorUserId);

        try
        {
            var countId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                 ?? throw new InvalidOperationException("No fue posible registrar el conteo fisico."));
            return await GetPhysicalInventoryCountAsync(organizationId, countId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar el conteo fisico registrado.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    private async Task<WarehouseRecord?> GetWarehouseAsync(Guid organizationId, Guid warehouseId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at
            from app.warehouses w
            where w.organization_id = @organization_id
              and w.warehouse_id = @warehouse_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("warehouse_id", warehouseId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWarehouse(reader, 0) : null;
    }

    private async Task<StockMovementRecord?> GetStockMovementAsync(Guid organizationId, Guid movementId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                sm.stock_movement_id,
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at,
                sm.product_id,
                sm.movement_type,
                sm.quantity,
                sm.quantity_delta,
                sm.resulting_quantity,
                sm.reason,
                sm.reference_document,
                sm.notes,
                sm.performed_by_user_id,
                sm.created_at
            from app.stock_movements sm
            join app.warehouses w on w.warehouse_id = sm.warehouse_id
            where sm.organization_id = @organization_id
              and sm.stock_movement_id = @stock_movement_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("stock_movement_id", movementId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadStockMovement(reader) : null;
    }

    private async Task<PhysicalInventoryCountRecord?> GetPhysicalInventoryCountAsync(Guid organizationId, Guid countId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                pic.physical_inventory_count_id,
                w.warehouse_id,
                w.organization_id,
                w.name,
                w.code,
                w.is_default,
                w.is_active,
                w.created_at,
                w.updated_at,
                pic.product_id,
                pic.expected_quantity,
                pic.counted_quantity,
                pic.difference_quantity,
                pic.reason,
                pic.notes,
                pic.performed_by_user_id,
                pic.created_at
            from app.physical_inventory_counts pic
            join app.warehouses w on w.warehouse_id = pic.warehouse_id
            where pic.organization_id = @organization_id
              and pic.physical_inventory_count_id = @count_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("count_id", countId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PhysicalInventoryCountRecord(
            reader.GetGuid(0),
            ReadWarehouse(reader, 1),
            reader.GetGuid(9),
            reader.GetDecimal(10),
            reader.GetDecimal(11),
            reader.GetDecimal(12),
            reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetGuid(15),
            reader.GetFieldValue<DateTimeOffset>(16));
    }

    private static StockItemRecord ReadStockItem(NpgsqlDataReader reader)
    {
        var warehouse = ReadWarehouse(reader, 0);

        ProductCategoryRecord? category = null;
        if (!reader.IsDBNull(16))
        {
            category = new ProductCategoryRecord(
                reader.GetGuid(16),
                reader.GetGuid(9),
                reader.GetString(17),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.GetBoolean(19));
        }

        BrandRecord? brand = null;
        if (!reader.IsDBNull(20))
        {
            brand = new BrandRecord(
                reader.GetGuid(20),
                reader.GetGuid(9),
                reader.GetString(21),
                reader.IsDBNull(22) ? null : reader.GetString(22),
                reader.GetBoolean(23));
        }

        var unit = new UnitOfMeasureRecord(
            reader.GetGuid(24),
            reader.GetGuid(9),
            reader.GetString(25),
            reader.GetString(26),
            reader.GetString(27),
            reader.GetBoolean(28),
            reader.GetBoolean(29));

        var policy = new StockPolicyRecord(
            reader.IsDBNull(31) ? null : reader.GetDecimal(31),
            reader.IsDBNull(32) ? null : reader.GetDecimal(32),
            reader.IsDBNull(33) ? null : reader.GetDecimal(33),
            reader.GetInt32(34),
            reader.GetFieldValue<DateTimeOffset>(35));

        return new StockItemRecord(
            warehouse,
            reader.GetGuid(8),
            reader.GetGuid(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.GetBoolean(14),
            reader.GetBoolean(15),
            category,
            brand,
            unit,
            reader.GetDecimal(30),
            reader.GetBoolean(37),
            reader.IsDBNull(36) ? null : reader.GetFieldValue<DateTimeOffset>(36),
            policy);
    }

    private static StockMovementRecord ReadStockMovement(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            ReadWarehouse(reader, 1),
            reader.GetGuid(9),
            reader.GetString(10),
            reader.GetDecimal(11),
            reader.GetDecimal(12),
            reader.GetDecimal(13),
            reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            reader.IsDBNull(17) ? null : reader.GetGuid(17),
            reader.GetFieldValue<DateTimeOffset>(18));

    private static WarehouseRecord ReadWarehouse(NpgsqlDataReader reader, int startIndex)
        => new(
            reader.GetGuid(startIndex),
            reader.GetGuid(startIndex + 1),
            reader.GetString(startIndex + 2),
            reader.GetString(startIndex + 3),
            reader.GetBoolean(startIndex + 4),
            reader.GetBoolean(startIndex + 5),
            reader.GetFieldValue<DateTimeOffset>(startIndex + 6),
            reader.GetFieldValue<DateTimeOffset>(startIndex + 7));

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value);
    }

    private static string ToSpanishMessage(string messageText)
    {
        return messageText switch
        {
            "The informed product does not belong to the active organization." => "El producto informado no pertenece a la organizacion activa.",
            "The informed warehouse does not belong to the active organization." => "El deposito informado no pertenece a la organizacion activa.",
            "The active organization does not have a default warehouse configured." => "La organizacion activa no tiene un deposito default configurado.",
            "The warehouse name is required." => "El nombre del deposito es obligatorio.",
            "The warehouse code is required." => "El codigo del deposito es obligatorio.",
            "The minimum quantity cannot be negative." => "El stock minimo no puede ser negativo.",
            "The maximum quantity cannot be negative." => "El stock maximo no puede ser negativo.",
            "The reorder point cannot be negative." => "El punto de reposicion no puede ser negativo.",
            "The counted quantity cannot be negative." => "La cantidad contada no puede ser negativa.",
            "The maximum quantity cannot be lower than the minimum quantity." => "El stock maximo no puede ser menor al stock minimo.",
            "The reorder point cannot be lower than the minimum quantity." => "El punto de reposicion no puede ser menor al stock minimo.",
            "The informed quantity must be greater than zero." => "La cantidad debe ser mayor a cero.",
            "The informed product does not allow fractioned stock quantities." => "El producto informado no permite cantidades fraccionadas.",
            "The informed stock movement type is not supported." => "El tipo de movimiento informado no es valido.",
            "There is not enough stock to register the requested outbound movement." => "No hay stock suficiente para registrar el egreso solicitado.",
            _ => "No fue posible persistir la informacion de stock informada."
        };
    }
}
