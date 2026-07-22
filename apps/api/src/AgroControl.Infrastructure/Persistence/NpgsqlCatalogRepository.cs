using AgroControl.Application.Auth;
using AgroControl.Application.Catalog;
using AgroControl.Application.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlCatalogRepository(ISqlConnectionFactory connectionFactory) : ICatalogRepository
{
    public async Task<ProductListResult> ListProductsAsync(Guid organizationId, ProductListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with filtered_products as (
                select
                    p.product_id,
                    p.organization_id,
                    p.name,
                    p.description,
                    p.internal_code,
                    p.sku,
                    p.barcode,
                    p.is_active,
                    p.allows_fraction,
                    p.sales_unit_label,
                    p.created_at,
                    p.updated_at,
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
                    pl.price_list_id,
                    pl.name as price_list_name,
                    pl.code as price_list_code,
                    pp.cost_amount,
                    pp.margin_percent,
                    pp.sale_amount,
                    pp.currency_code,
                    pp.effective_from,
                    count(*) over() as total_count
                from app.products p
                left join app.product_categories c on c.category_id = p.category_id
                left join app.brands b on b.brand_id = p.brand_id
                join app.units_of_measure u on u.unit_id = p.base_unit_id
                join app.price_lists pl
                    on pl.organization_id = p.organization_id
                   and pl.is_default
                   and pl.is_active
                join app.product_prices pp
                    on pp.product_id = p.product_id
                   and pp.price_list_id = pl.price_list_id
                where p.organization_id = @organization_id
                  and (@search is null
                       or p.name ilike '%' || @search || '%'
                       or p.internal_code ilike '%' || @search || '%'
                       or coalesce(p.sku, '') ilike '%' || @search || '%'
                       or coalesce(p.barcode, '') ilike '%' || @search || '%')
                  and (@category_id is null or p.category_id = @category_id)
                  and (@brand_id is null or p.brand_id = @brand_id)
                  and (@is_active is null or p.is_active = @is_active)
            )
            select *
            from filtered_products
            order by name asc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "search", query.Search);
        AddNullable(command, "category_id", query.CategoryId);
        AddNullable(command, "brand_id", query.BrandId);
        AddNullable(command, "is_active", query.IsActive);
        command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<ProductRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(ReadProduct(reader));
            total = reader.GetInt32(34);
        }

        return new ProductListResult(products, query.Page, query.PageSize, total);
    }

    public async Task<ProductRecord?> GetProductAsync(Guid organizationId, Guid productId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                p.product_id,
                p.organization_id,
                p.name,
                p.description,
                p.internal_code,
                p.sku,
                p.barcode,
                p.is_active,
                p.allows_fraction,
                p.sales_unit_label,
                p.created_at,
                p.updated_at,
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
                pl.price_list_id,
                pl.name as price_list_name,
                pl.code as price_list_code,
                pp.cost_amount,
                pp.margin_percent,
                pp.sale_amount,
                pp.currency_code,
                pp.effective_from
            from app.products p
            left join app.product_categories c on c.category_id = p.category_id
            left join app.brands b on b.brand_id = p.brand_id
            join app.units_of_measure u on u.unit_id = p.base_unit_id
            join app.price_lists pl
                on pl.organization_id = p.organization_id
               and pl.is_default
               and pl.is_active
            join app.product_prices pp
                on pp.product_id = p.product_id
               and pp.price_list_id = pl.price_list_id
            where p.organization_id = @organization_id
              and p.product_id = @product_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("product_id", productId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadProduct(reader)
            : null;
    }

    public async Task<ProductRecord> CreateProductAsync(Guid organizationId, CreateProductCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.create_product(
                @organization_id,
                @category_id,
                @brand_id,
                @base_unit_id,
                @name,
                @description,
                @internal_code,
                @sku,
                @barcode,
                @allows_fraction,
                @sales_unit_label,
                @cost_amount,
                @margin_percent,
                @sale_amount,
                @currency_code,
                @price_list_id);
            """;
        AddProductParameters(dbCommand, organizationId, command);

        try
        {
            var productId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                   ?? throw new InvalidOperationException("No fue posible crear el producto."));

            return (await GetProductAsync(organizationId, productId, cancellationToken))!;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(exception.MessageText);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<ProductRecord?> UpdateProductAsync(Guid organizationId, Guid productId, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var currentProduct = await GetProductAsync(organizationId, productId, cancellationToken);

        if (currentProduct is null)
        {
            return null;
        }

        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.update_product(
                @organization_id,
                @product_id,
                @category_id,
                @brand_id,
                @base_unit_id,
                @name,
                @description,
                @internal_code,
                @sku,
                @barcode,
                @is_active,
                @allows_fraction,
                @sales_unit_label,
                @cost_amount,
                @margin_percent,
                @sale_amount,
                @currency_code,
                @price_list_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("product_id", productId);
        AddNullable(dbCommand, "category_id", command.CategoryId);
        AddNullable(dbCommand, "brand_id", command.BrandId);
        dbCommand.Parameters.AddWithValue("base_unit_id", command.BaseUnitId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);
        dbCommand.Parameters.AddWithValue("internal_code", command.InternalCode);
        AddNullable(dbCommand, "sku", command.Sku);
        AddNullable(dbCommand, "barcode", command.Barcode);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);
        dbCommand.Parameters.AddWithValue("allows_fraction", command.AllowsFraction);
        AddNullable(dbCommand, "sales_unit_label", command.SalesUnitLabel);
        dbCommand.Parameters.AddWithValue("cost_amount", command.CostAmount);
        AddNullable(dbCommand, "margin_percent", command.MarginPercent);
        dbCommand.Parameters.AddWithValue("sale_amount", command.SaleAmount);
        dbCommand.Parameters.AddWithValue("currency_code", command.CurrencyCode);
        AddNullable(dbCommand, "price_list_id", command.PriceListId);

        try
        {
            var updatedId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                   ?? throw new InvalidOperationException("No fue posible actualizar el producto."));

            return await GetProductAsync(organizationId, updatedId, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(exception.MessageText);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<IReadOnlyList<ProductCategoryRecord>> ListCategoriesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await ListSimpleAsync(
            organizationId,
            """
            select category_id, organization_id, name, description, is_active
            from app.product_categories
            where organization_id = @organization_id
            order by name asc;
            """,
            static reader => new ProductCategoryRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetBoolean(4)),
            cancellationToken);
    }

    public async Task<ProductCategoryRecord> CreateCategoryAsync(Guid organizationId, CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            insert into app.product_categories (
                category_id,
                organization_id,
                name,
                description,
                is_active,
                created_at,
                updated_at)
            values (
                @category_id,
                @organization_id,
                @name,
                @description,
                true,
                timezone('utc', now()),
                timezone('utc', now()))
            returning category_id, organization_id, name, description, is_active;
            """;
        var categoryId = Guid.NewGuid();
        dbCommand.Parameters.AddWithValue("category_id", categoryId);
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return new ProductCategoryRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<ProductCategoryRecord?> UpdateCategoryAsync(Guid organizationId, Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            update app.product_categories
            set name = @name,
                description = @description,
                is_active = @is_active,
                updated_at = timezone('utc', now())
            where organization_id = @organization_id
              and category_id = @category_id
            returning category_id, organization_id, name, description, is_active;
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("category_id", categoryId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? new ProductCategoryRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4))
                : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<IReadOnlyList<BrandRecord>> ListBrandsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await ListSimpleAsync(
            organizationId,
            """
            select brand_id, organization_id, name, description, is_active
            from app.brands
            where organization_id = @organization_id
            order by name asc;
            """,
            static reader => new BrandRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetBoolean(4)),
            cancellationToken);
    }

    public async Task<BrandRecord> CreateBrandAsync(Guid organizationId, CreateBrandCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            insert into app.brands (
                brand_id,
                organization_id,
                name,
                description,
                is_active,
                created_at,
                updated_at)
            values (
                @brand_id,
                @organization_id,
                @name,
                @description,
                true,
                timezone('utc', now()),
                timezone('utc', now()))
            returning brand_id, organization_id, name, description, is_active;
            """;
        var brandId = Guid.NewGuid();
        dbCommand.Parameters.AddWithValue("brand_id", brandId);
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return new BrandRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<BrandRecord?> UpdateBrandAsync(Guid organizationId, Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            update app.brands
            set name = @name,
                description = @description,
                is_active = @is_active,
                updated_at = timezone('utc', now())
            where organization_id = @organization_id
              and brand_id = @brand_id
            returning brand_id, organization_id, name, description, is_active;
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("brand_id", brandId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? new BrandRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4))
                : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<IReadOnlyList<UnitOfMeasureRecord>> ListUnitsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await ListSimpleAsync(
            organizationId,
            """
            select unit_id, organization_id, name, code, symbol, allows_fraction, is_active
            from app.units_of_measure
            where organization_id = @organization_id
            order by name asc;
            """,
            static reader => new UnitOfMeasureRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<PriceListRecord>> ListPriceListsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await ListSimpleAsync(
            organizationId,
            """
            select price_list_id, organization_id, name, code, is_default, is_active
            from app.price_lists
            where organization_id = @organization_id
            order by is_default desc, name asc;
            """,
            static reader => new PriceListRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5)),
            cancellationToken);
    }

    public async Task<PriceListRecord> CreatePriceListAsync(Guid organizationId, CreatePriceListCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (command.IsDefault)
        {
            await ResetDefaultPriceListsAsync(connection, transaction, organizationId, cancellationToken);
        }

        await using var dbCommand = connection.CreateCommand();
        dbCommand.Transaction = transaction;
        dbCommand.CommandText =
            """
            insert into app.price_lists (
                price_list_id,
                organization_id,
                name,
                code,
                is_default,
                is_active,
                created_at,
                updated_at)
            values (
                @price_list_id,
                @organization_id,
                @name,
                @code,
                @is_default,
                @is_active,
                timezone('utc', now()),
                timezone('utc', now()))
            returning price_list_id, organization_id, name, code, is_default, is_active;
            """;
        var priceListId = Guid.NewGuid();
        dbCommand.Parameters.AddWithValue("price_list_id", priceListId);
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        dbCommand.Parameters.AddWithValue("code", command.Code);
        dbCommand.Parameters.AddWithValue("is_default", command.IsDefault);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            var result = new PriceListRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetBoolean(5));
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw ToValidationException(exception);
        }
    }

    public async Task<PriceListRecord?> UpdatePriceListAsync(Guid organizationId, Guid priceListId, UpdatePriceListCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (command.IsDefault)
        {
            await ResetDefaultPriceListsAsync(connection, transaction, organizationId, cancellationToken);
        }

        await using var dbCommand = connection.CreateCommand();
        dbCommand.Transaction = transaction;
        dbCommand.CommandText =
            """
            update app.price_lists
            set name = @name,
                code = @code,
                is_default = @is_default,
                is_active = @is_active,
                updated_at = timezone('utc', now())
            where organization_id = @organization_id
              and price_list_id = @price_list_id
            returning price_list_id, organization_id, name, code, is_default, is_active;
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("price_list_id", priceListId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        dbCommand.Parameters.AddWithValue("code", command.Code);
        dbCommand.Parameters.AddWithValue("is_default", command.IsDefault);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);

        try
        {
            await using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var result = new PriceListRecord(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetBoolean(5));
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw ToValidationException(exception);
        }
    }

    private async Task<IReadOnlyList<T>> ListSimpleAsync<T>(
        Guid organizationId,
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("organization_id", organizationId);

        var items = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        return items;
    }

    private static ProductRecord ReadProduct(NpgsqlDataReader reader)
    {
        ProductCategoryRecord? category = null;
        if (!reader.IsDBNull(12))
        {
            category = new ProductCategoryRecord(
                reader.GetGuid(12),
                reader.GetGuid(1),
                reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.GetBoolean(15));
        }

        BrandRecord? brand = null;
        if (!reader.IsDBNull(16))
        {
            brand = new BrandRecord(
                reader.GetGuid(16),
                reader.GetGuid(1),
                reader.GetString(17),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.GetBoolean(19));
        }

        var unit = new UnitOfMeasureRecord(
            reader.GetGuid(20),
            reader.GetGuid(1),
            reader.GetString(21),
            reader.GetString(22),
            reader.GetString(23),
            reader.GetBoolean(24),
            reader.GetBoolean(25));

        var priceOffset = reader.FieldCount == 35 ? 26 : 26;
        var price = new ProductPriceRecord(
            reader.GetGuid(priceOffset),
            reader.GetString(priceOffset + 1),
            reader.GetString(priceOffset + 2),
            reader.GetDecimal(priceOffset + 3),
            reader.IsDBNull(priceOffset + 4) ? null : reader.GetDecimal(priceOffset + 4),
            reader.GetDecimal(priceOffset + 5),
            reader.GetString(priceOffset + 6),
            reader.GetFieldValue<DateTimeOffset>(priceOffset + 7));

        return new ProductRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetBoolean(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            category,
            brand,
            unit,
            price,
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }

    private static void AddProductParameters(NpgsqlCommand dbCommand, Guid organizationId, CreateProductCommand command)
    {
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(dbCommand, "category_id", command.CategoryId);
        AddNullable(dbCommand, "brand_id", command.BrandId);
        dbCommand.Parameters.AddWithValue("base_unit_id", command.BaseUnitId);
        dbCommand.Parameters.AddWithValue("name", command.Name);
        AddNullable(dbCommand, "description", command.Description);
        dbCommand.Parameters.AddWithValue("internal_code", command.InternalCode);
        AddNullable(dbCommand, "sku", command.Sku);
        AddNullable(dbCommand, "barcode", command.Barcode);
        dbCommand.Parameters.AddWithValue("allows_fraction", command.AllowsFraction);
        AddNullable(dbCommand, "sales_unit_label", command.SalesUnitLabel);
        dbCommand.Parameters.AddWithValue("cost_amount", command.CostAmount);
        AddNullable(dbCommand, "margin_percent", command.MarginPercent);
        dbCommand.Parameters.AddWithValue("sale_amount", command.SaleAmount);
        dbCommand.Parameters.AddWithValue("currency_code", command.CurrencyCode);
        AddNullable(dbCommand, "price_list_id", command.PriceListId);
    }

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        NpgsqlParameterHelper.AddNullable(command, name, value);
    }

    private static ValidationException ToValidationException(PostgresException exception)
    {
        if (exception.ConstraintName?.Contains("internal_code", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe un producto con ese codigo interno.");
        }

        if (exception.ConstraintName?.Contains("sku", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe un producto con ese SKU.");
        }

        if (exception.ConstraintName?.Contains("barcode", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe un producto con ese codigo de barras.");
        }

        if (exception.ConstraintName?.Contains("price_lists", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe una lista de precios con ese codigo.");
        }

        if (exception.ConstraintName?.Contains("categories", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe una categoria con ese nombre.");
        }

        if (exception.ConstraintName?.Contains("brands", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe una marca con ese nombre.");
        }

        return new ValidationException("No fue posible persistir la informacion informada.");
    }

    private static async Task ResetDefaultPriceListsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            update app.price_lists
            set is_default = false,
                updated_at = timezone('utc', now())
            where organization_id = @organization_id
              and is_default;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
