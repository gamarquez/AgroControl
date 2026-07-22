using AgroControl.Application.Auth;
using AgroControl.Application.Persistence;
using AgroControl.Application.Sales;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlSalesRepository(ISqlConnectionFactory connectionFactory) : ISalesRepository
{
    public async Task<PosProductListResult> ListPosProductsAsync(Guid organizationId, PosProductListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with default_warehouse as (
                select w.warehouse_id
                from app.warehouses w
                where w.organization_id = @organization_id
                  and w.is_default = true
                  and w.is_active = true
                order by w.created_at asc
                limit 1
            ),
            filtered_products as (
                select
                    p.product_id,
                    p.name,
                    p.internal_code,
                    p.sku,
                    p.barcode,
                    p.allows_fraction,
                    u.symbol as unit_symbol,
                    coalesce(sb.on_hand_quantity, 0) as on_hand_quantity,
                    pp.sale_amount,
                    pp.currency_code,
                    count(*) over() as total_count
                from default_warehouse dw
                join app.products p on p.organization_id = @organization_id
                join app.units_of_measure u on u.unit_id = p.base_unit_id
                join app.price_lists pl
                    on pl.organization_id = p.organization_id
                   and pl.is_default
                   and pl.is_active
                join app.product_prices pp
                    on pp.product_id = p.product_id
                   and pp.price_list_id = pl.price_list_id
                left join app.stock_balances sb
                    on sb.organization_id = p.organization_id
                   and sb.warehouse_id = dw.warehouse_id
                   and sb.product_id = p.product_id
                where p.is_active = true
                  and (@search is null
                       or p.name ilike '%' || @search || '%'
                       or p.internal_code ilike '%' || @search || '%'
                       or coalesce(p.sku, '') ilike '%' || @search || '%'
                       or coalesce(p.barcode, '') ilike '%' || @search || '%')
            )
            select *
            from filtered_products
            order by name asc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "search", query.Search);
        command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<PosProductRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PosProductRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetBoolean(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetString(9)));
            total = reader.GetInt32(10);
        }

        return new PosProductListResult(items, query.Page, query.PageSize, total);
    }

    public async Task<SaleListResult> ListSalesAsync(Guid organizationId, SaleListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                s.sale_id,
                s.ticket_number,
                s.cash_session_id,
                s.customer_id,
                s.customer_name,
                s.sale_channel,
                s.status,
                s.total_amount,
                s.paid_amount,
                s.account_balance_amount,
                s.credit_balance_applied_amount,
                s.due_date,
                s.currency_code,
                count(si.sale_item_id) as item_count,
                s.created_at,
                count(*) over() as total_count
            from app.sales s
            left join app.sale_items si on si.sale_id = s.sale_id
            where s.organization_id = @organization_id
              and (@search is null
                   or s.customer_name ilike '%' || @search || '%'
                   or s.ticket_number::text ilike '%' || @search || '%')
              and (@status is null or s.status = @status)
              and (@customer_id is null or s.customer_id = @customer_id)
            group by s.sale_id
            order by s.created_at desc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "search", query.Search);
        AddNullable(command, "status", query.Status);
        AddNullable(command, "customer_id", query.CustomerId);
        command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<SaleSummaryRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SaleSummaryRecord(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetDecimal(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.IsDBNull(11) ? null : DateOnly.FromDateTime(reader.GetDateTime(11)),
                reader.GetString(12),
                reader.GetInt32(13),
                reader.GetFieldValue<DateTimeOffset>(14)));
            total = reader.GetInt32(15);
        }

        return new SaleListResult(items, query.Page, query.PageSize, total);
    }

    public async Task<SaleRecord?> GetSaleAsync(Guid organizationId, Guid saleId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                s.sale_id,
                s.ticket_number,
                s.organization_id,
                s.cash_session_id,
                s.customer_id,
                s.sold_by_user_id,
                s.sale_channel,
                s.status,
                s.customer_name,
                s.subtotal_amount,
                s.discount_amount,
                s.total_amount,
                s.paid_amount,
                s.account_balance_amount,
                s.credit_balance_applied_amount,
                s.due_date,
                s.currency_code,
                s.reversal_cash_session_id,
                s.reversed_by_user_id,
                s.reversed_at,
                s.notes,
                s.reversal_notes,
                s.created_at,
                si.sale_item_id,
                si.product_id,
                si.product_name,
                si.unit_symbol,
                si.quantity,
                coalesce(sir.returned_quantity, 0) as returned_quantity,
                si.quantity - coalesce(sir.returned_quantity, 0) as available_to_return_quantity,
                si.unit_price,
                si.line_total
            from app.sales s
            left join app.sale_items si on si.sale_id = s.sale_id
            left join lateral (
                select sum(sri.quantity) as returned_quantity
                from app.sale_return_items sri
                where sri.sale_item_id = si.sale_item_id
            ) sir on true
            where s.organization_id = @organization_id
              and s.sale_id = @sale_id
            order by si.created_at asc nulls last;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("sale_id", saleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        SaleRecord? sale = null;
        var items = new List<SaleItemRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            sale ??= new SaleRecord(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetGuid(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.GetGuid(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetDecimal(9),
                reader.GetDecimal(10),
                reader.GetDecimal(11),
                reader.GetDecimal(12),
                reader.GetDecimal(13),
                reader.GetDecimal(14),
                reader.IsDBNull(15) ? null : DateOnly.FromDateTime(reader.GetDateTime(15)),
                reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetGuid(17),
                reader.IsDBNull(18) ? null : reader.GetGuid(18),
                reader.IsDBNull(19) ? null : reader.GetFieldValue<DateTimeOffset>(19),
                reader.IsDBNull(20) ? null : reader.GetString(20),
                reader.IsDBNull(21) ? null : reader.GetString(21),
                reader.GetFieldValue<DateTimeOffset>(22),
                items,
                [],
                []);

            if (!reader.IsDBNull(23))
            {
                items.Add(new SaleItemRecord(
                    reader.GetGuid(23),
                    reader.GetGuid(24),
                    reader.GetString(25),
                    reader.GetString(26),
                    reader.GetDecimal(27),
                    reader.GetDecimal(28),
                    reader.GetDecimal(29),
                    reader.GetDecimal(30),
                    reader.GetDecimal(31)));
            }
        }

        if (sale is not null)
        {
            sale = sale with
            {
                Payments = await GetSalePaymentsAsync(connection, organizationId, saleId, cancellationToken),
                Returns = await GetSaleReturnsAsync(connection, organizationId, saleId, cancellationToken)
            };
        }

        return sale;
    }

    public async Task<SaleRecord> CreateCashSaleAsync(Guid organizationId, Guid actorUserId, CreateCashSaleCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.create_cash_sale(
                @organization_id,
                @cash_session_id,
                @sold_by_user_id,
                @items,
                @notes);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("cash_session_id", command.CashSessionId);
        dbCommand.Parameters.AddWithValue("sold_by_user_id", actorUserId);
        var itemsJson = JsonSerializer.Serialize(command.Items.Select(item => new
        {
            productId = item.ProductId,
            quantity = item.Quantity
        }));
        dbCommand.Parameters.Add(new NpgsqlParameter("items", NpgsqlDbType.Jsonb) { Value = itemsJson });
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            var saleId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                ?? throw new InvalidOperationException("No fue posible registrar la venta."));

            return await GetSaleAsync(organizationId, saleId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la venta registrada.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<SaleRecord> CreateAccountSaleAsync(Guid organizationId, Guid actorUserId, CreateAccountSaleCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.create_account_sale(
                @organization_id,
                @customer_id,
                @sold_by_user_id,
                @items,
                @due_date,
                @notes);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("customer_id", command.CustomerId);
        dbCommand.Parameters.AddWithValue("sold_by_user_id", actorUserId);
        var itemsJson = JsonSerializer.Serialize(command.Items.Select(item => new
        {
            productId = item.ProductId,
            quantity = item.Quantity
        }));
        dbCommand.Parameters.Add(new NpgsqlParameter("items", NpgsqlDbType.Jsonb) { Value = itemsJson });
        AddNullable(dbCommand, "due_date", command.DueDate?.ToDateTime(TimeOnly.MinValue));
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            var saleId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                ?? throw new InvalidOperationException("No fue posible registrar la venta a cuenta."));

            return await GetSaleAsync(organizationId, saleId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la venta a cuenta registrada.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<SaleRecord> CreateCheckoutSaleAsync(Guid organizationId, Guid actorUserId, CreateCheckoutSaleCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.create_checkout_sale(
                @organization_id,
                @cash_session_id,
                @customer_id,
                @sold_by_user_id,
                @items,
                @payments,
                @due_date,
                @notes);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(dbCommand, "cash_session_id", command.CashSessionId);
        AddNullable(dbCommand, "customer_id", command.CustomerId);
        dbCommand.Parameters.AddWithValue("sold_by_user_id", actorUserId);
        var itemsJson = JsonSerializer.Serialize(command.Items.Select(item => new
        {
            productId = item.ProductId,
            quantity = item.Quantity
        }));
        dbCommand.Parameters.Add(new NpgsqlParameter("items", NpgsqlDbType.Jsonb) { Value = itemsJson });
        var paymentsJson = JsonSerializer.Serialize(command.Payments.Select(payment => new
        {
            paymentMethod = payment.PaymentMethod,
            amount = payment.Amount,
            reference = payment.Reference,
            providerName = payment.ProviderName
        }));
        dbCommand.Parameters.Add(new NpgsqlParameter("payments", NpgsqlDbType.Jsonb) { Value = paymentsJson });
        AddNullable(dbCommand, "due_date", command.DueDate?.ToDateTime(TimeOnly.MinValue));
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            var saleId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                ?? throw new InvalidOperationException("No fue posible registrar el checkout."));

            return await GetSaleAsync(organizationId, saleId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar el checkout registrado.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<SaleRecord> ReverseCashSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReverseCashSaleCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.reverse_cash_sale(
                @organization_id,
                @sale_id,
                @cash_session_id,
                @reversed_by_user_id,
                @reversal_notes);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("sale_id", saleId);
        dbCommand.Parameters.AddWithValue("cash_session_id", command.CashSessionId);
        dbCommand.Parameters.AddWithValue("reversed_by_user_id", actorUserId);
        AddNullable(dbCommand, "reversal_notes", command.ReversalNotes);

        try
        {
            var reversedSaleId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                        ?? throw new InvalidOperationException("No fue posible revertir la venta."));

            return await GetSaleAsync(organizationId, reversedSaleId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la venta revertida.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<SaleRecord> ReturnSaleAsync(Guid organizationId, Guid actorUserId, Guid saleId, ReturnSaleCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.return_sale_items(
                @organization_id,
                @sale_id,
                @cash_session_id,
                @returned_by_user_id,
                @items,
                @notes);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("sale_id", saleId);
        AddNullable(dbCommand, "cash_session_id", command.CashSessionId);
        dbCommand.Parameters.AddWithValue("returned_by_user_id", actorUserId);
        var itemsJson = JsonSerializer.Serialize(command.Items.Select(item => new
        {
            saleItemId = item.SaleItemId,
            quantity = item.Quantity
        }));
        dbCommand.Parameters.Add(new NpgsqlParameter("items", NpgsqlDbType.Jsonb) { Value = itemsJson });
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            _ = await dbCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("No fue posible registrar la devolucion parcial.");

            return await GetSaleAsync(organizationId, saleId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la venta luego de la devolucion parcial.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        NpgsqlParameterHelper.AddNullable(command, name, value);
    }

    private static async Task<IReadOnlyList<SalePaymentRecord>> GetSalePaymentsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid saleId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                sp.sale_payment_id,
                sp.payment_method,
                sp.amount,
                sp.reference,
                sp.provider_name,
                sp.created_at
            from app.sale_payments sp
            where sp.organization_id = @organization_id
              and sp.sale_id = @sale_id
            order by sp.created_at asc, sp.sale_payment_id asc;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("sale_id", saleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<SalePaymentRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SalePaymentRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return items;
    }

    private static async Task<IReadOnlyList<SaleReturnRecord>> GetSaleReturnsAsync(
        NpgsqlConnection connection,
        Guid organizationId,
        Guid saleId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                sr.sale_return_id,
                sr.sale_id,
                sr.cash_session_id,
                sr.customer_account_movement_id,
                sr.returned_by_user_id,
                sr.return_total_amount,
                sr.refunded_paid_amount,
                sr.credited_account_amount,
                sr.notes,
                sr.created_at,
                sri.sale_return_item_id,
                sri.sale_item_id,
                sri.product_id,
                sri.quantity,
                sri.unit_price,
                sri.line_total,
                sri.created_at
            from app.sale_returns sr
            left join app.sale_return_items sri on sri.sale_return_id = sr.sale_return_id
            where sr.organization_id = @organization_id
              and sr.sale_id = @sale_id
            order by sr.created_at desc, sri.created_at asc nulls last;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("sale_id", saleId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var returns = new List<SaleReturnRecord>();
        var itemsByReturnId = new Dictionary<Guid, List<SaleReturnItemRecord>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var saleReturnId = reader.GetGuid(0);
            if (!itemsByReturnId.ContainsKey(saleReturnId))
            {
                itemsByReturnId[saleReturnId] = [];
                returns.Add(new SaleReturnRecord(
                    saleReturnId,
                    reader.GetGuid(1),
                    reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    reader.GetGuid(4),
                    reader.GetDecimal(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.GetFieldValue<DateTimeOffset>(9),
                    itemsByReturnId[saleReturnId]));
            }

            if (!reader.IsDBNull(10))
            {
                itemsByReturnId[saleReturnId].Add(new SaleReturnItemRecord(
                    reader.GetGuid(10),
                    reader.GetGuid(11),
                    reader.GetGuid(12),
                    reader.GetDecimal(13),
                    reader.GetDecimal(14),
                    reader.GetDecimal(15),
                    reader.GetFieldValue<DateTimeOffset>(16)));
            }
        }

        return returns;
    }

    private static string ToSpanishMessage(string messageText)
    {
        return messageText switch
        {
            "At least one sale item is required." => "Debe informar al menos un producto para la venta.",
            "At least one payment component is required." => "Debe informar al menos un medio de pago.",
            "The informed cash session is not open for the active organization." => "La sesion de caja informada no esta abierta para la organizacion activa.",
            "The informed cash session is required for collected payment methods." => "La sesion de caja es obligatoria cuando hay pagos cobrados en caja.",
            "The informed customer does not belong to the active organization." => "El cliente informado no pertenece a la organizacion activa.",
            "A customer is required to use account payment." => "Debes seleccionar un cliente para usar cuenta corriente.",
            "Each sale item must include a valid product." => "Cada item de la venta debe incluir un producto valido.",
            "Each sale item quantity must be greater than zero." => "Cada item de la venta debe tener una cantidad mayor a cero.",
            "One of the informed payment methods is not supported." => "Uno de los medios de pago informados no esta soportado.",
            "Each payment amount must be greater than zero." => "Cada importe de pago debe ser mayor a cero.",
            "One of the informed products is not available for sale in the active organization." => "Uno de los productos informados no esta disponible para la venta en la organizacion activa.",
            "One of the informed products does not allow fractioned quantities." => "Uno de los productos informados no permite cantidades fraccionadas.",
            "There is not enough stock to confirm the sale." => "No hay stock suficiente para confirmar la venta.",
            "The total amount of payments does not match the sale total." => "La suma de medios de pago no coincide con el total de la venta.",
            "The customer would exceed the configured credit limit." => "La venta supera el limite de credito configurado para el cliente.",
            "The informed due date cannot be in the past." => "La fecha de vencimiento no puede estar en el pasado.",
            "A due date is required when the sale includes account balance." => "Debes informar un vencimiento cuando parte de la venta queda en cuenta corriente.",
            "The informed sale does not belong to the active organization." => "La venta informada no pertenece a la organizacion activa.",
            "Only confirmed sales can be reversed." => "Solo puedes revertir ventas confirmadas.",
            "Only confirmed sales can receive partial returns." => "Solo puedes devolver items de ventas confirmadas.",
            "At least one return item is required." => "Debe informar al menos un item para la devolucion.",
            "Each return item must include a valid sale item." => "Cada item de la devolucion debe referenciar un item de venta valido.",
            "Each return item quantity must be greater than zero." => "Cada cantidad a devolver debe ser mayor a cero.",
            "One of the informed sale items does not belong to the sale." => "Uno de los items informados no pertenece a la venta.",
            "The informed return quantity exceeds the available quantity for the sale item." => "La cantidad a devolver supera lo disponible para ese item.",
            "The partial return total must be greater than zero." => "El total de la devolucion parcial debe ser mayor a cero.",
            "The informed sale cannot be partially returned." => "La venta informada no admite devolucion parcial.",
            "An open cash session is required to refund collected amounts." => "Debes informar una caja abierta para reintegrar importes cobrados.",
            "There is not enough balance to refund the informed partial return." => "No hay saldo suficiente en caja para reintegrar la devolucion parcial.",
            "The informed sale does not support account credit adjustments." => "La venta informada no admite ajuste en cuenta corriente.",
            "The partial return credit exceeds the outstanding customer balance." => "La nota de credito por devolucion supera el saldo pendiente del cliente.",
            "There is not enough balance to reverse the informed sale." => "No hay saldo suficiente para revertir la venta informada.",
            _ => "No fue posible registrar la venta informada."
        };
    }
}
