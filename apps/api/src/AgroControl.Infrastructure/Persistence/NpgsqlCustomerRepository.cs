using AgroControl.Application.Auth;
using AgroControl.Application.Customers;
using AgroControl.Application.Persistence;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlCustomerRepository(ISqlConnectionFactory connectionFactory) : ICustomerRepository
{
    public async Task<CustomerListResult> ListCustomersAsync(Guid organizationId, CustomerListQuery query, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with customer_balances as (
                select
                    c.customer_id,
                    c.organization_id,
                    c.display_name,
                    c.tax_id,
                    c.phone,
                    c.email,
                    c.address,
                    c.credit_limit_amount,
                    c.is_active,
                    c.notes,
                    c.created_at,
                    c.updated_at,
                    coalesce(rb.resulting_balance, 0) as current_balance,
                    coalesce(ob.overdue_balance, 0) as overdue_balance,
                    nd.next_due_date,
                    count(*) over() as total_count
                from app.customers c
                left join lateral (
                    select resulting_balance
                    from app.customer_account_movements
                    where organization_id = c.organization_id
                      and customer_id = c.customer_id
                    order by created_at desc, customer_account_movement_id desc
                    limit 1
                ) rb on true
                left join lateral (
                    select sum(debit_open.open_amount) as overdue_balance
                    from (
                        select
                            cam.customer_account_movement_id,
                            greatest(
                                cam.debit_amount - coalesce(sum(cpa.applied_amount), 0),
                                0
                            ) as open_amount
                        from app.customer_account_movements cam
                        left join app.customer_payment_allocations cpa
                            on cpa.debit_movement_id = cam.customer_account_movement_id
                        where cam.organization_id = c.organization_id
                          and cam.customer_id = c.customer_id
                          and cam.movement_type = 'sale_debit'
                          and cam.due_date is not null
                          and cam.due_date < current_date
                        group by cam.customer_account_movement_id, cam.debit_amount
                    ) debit_open
                    where debit_open.open_amount > 0
                ) ob on true
                left join lateral (
                    select min(cam.due_date) as next_due_date
                    from (
                        select
                            cam.due_date,
                            greatest(
                                cam.debit_amount - coalesce(sum(cpa.applied_amount), 0),
                                0
                            ) as open_amount
                        from app.customer_account_movements cam
                        left join app.customer_payment_allocations cpa
                            on cpa.debit_movement_id = cam.customer_account_movement_id
                        where cam.organization_id = c.organization_id
                          and cam.customer_id = c.customer_id
                          and cam.movement_type = 'sale_debit'
                          and cam.due_date is not null
                        group by cam.customer_account_movement_id, cam.due_date, cam.debit_amount
                    ) cam
                    where cam.open_amount > 0
                ) nd on true
                where c.organization_id = @organization_id
                  and (@search is null
                       or c.display_name ilike '%' || @search || '%'
                       or coalesce(c.tax_id, '') ilike '%' || @search || '%'
                       or coalesce(c.phone, '') ilike '%' || @search || '%'
                       or coalesce(c.email, '') ilike '%' || @search || '%')
                  and (@is_active is null or c.is_active = @is_active)
            )
            select *
            from customer_balances
            order by overdue_balance desc, display_name asc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "search", query.Search);
        AddNullable(command, "is_active", query.IsActive);
        command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
        command.Parameters.AddWithValue("page_size", query.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CustomerRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadCustomer(reader));
            total = reader.GetInt32(15);
        }

        return new CustomerListResult(items, query.Page, query.PageSize, total);
    }

    public async Task<CustomerRecord?> GetCustomerAsync(Guid organizationId, Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            with customer_balances as (
                select
                    c.customer_id,
                    c.organization_id,
                    c.display_name,
                    c.tax_id,
                    c.phone,
                    c.email,
                    c.address,
                    c.credit_limit_amount,
                    c.is_active,
                    c.notes,
                    c.created_at,
                    c.updated_at,
                    coalesce(rb.resulting_balance, 0) as current_balance,
                    coalesce(ob.overdue_balance, 0) as overdue_balance,
                    nd.next_due_date
                from app.customers c
                left join lateral (
                    select resulting_balance
                    from app.customer_account_movements
                    where organization_id = c.organization_id
                      and customer_id = c.customer_id
                    order by created_at desc, customer_account_movement_id desc
                    limit 1
                ) rb on true
                left join lateral (
                    select sum(debit_open.open_amount) as overdue_balance
                    from (
                        select
                            cam.customer_account_movement_id,
                            greatest(
                                cam.debit_amount - coalesce(sum(cpa.applied_amount), 0),
                                0
                            ) as open_amount
                        from app.customer_account_movements cam
                        left join app.customer_payment_allocations cpa
                            on cpa.debit_movement_id = cam.customer_account_movement_id
                        where cam.organization_id = c.organization_id
                          and cam.customer_id = c.customer_id
                          and cam.movement_type = 'sale_debit'
                          and cam.due_date is not null
                          and cam.due_date < current_date
                        group by cam.customer_account_movement_id, cam.debit_amount
                    ) debit_open
                    where debit_open.open_amount > 0
                ) ob on true
                left join lateral (
                    select min(cam.due_date) as next_due_date
                    from (
                        select
                            cam.due_date,
                            greatest(
                                cam.debit_amount - coalesce(sum(cpa.applied_amount), 0),
                                0
                            ) as open_amount
                        from app.customer_account_movements cam
                        left join app.customer_payment_allocations cpa
                            on cpa.debit_movement_id = cam.customer_account_movement_id
                        where cam.organization_id = c.organization_id
                          and cam.customer_id = c.customer_id
                          and cam.movement_type = 'sale_debit'
                          and cam.due_date is not null
                        group by cam.customer_account_movement_id, cam.due_date, cam.debit_amount
                    ) cam
                    where cam.open_amount > 0
                ) nd on true
                where c.organization_id = @organization_id
                  and c.customer_id = @customer_id
            )
            select *
            from customer_balances;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("customer_id", customerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCustomer(reader) : null;
    }

    public async Task<CustomerRecord> CreateCustomerAsync(Guid organizationId, CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            insert into app.customers (
                customer_id,
                organization_id,
                display_name,
                tax_id,
                phone,
                email,
                address,
                credit_limit_amount,
                is_active,
                notes,
                created_at,
                updated_at
            )
            values (
                @customer_id,
                @organization_id,
                @display_name,
                @tax_id,
                @phone,
                @email,
                @address,
                @credit_limit_amount,
                true,
                @notes,
                timezone('utc', now()),
                timezone('utc', now())
            );
            """;
        var customerId = Guid.NewGuid();
        dbCommand.Parameters.AddWithValue("customer_id", customerId);
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("display_name", command.DisplayName);
        AddNullable(dbCommand, "tax_id", command.TaxId);
        AddNullable(dbCommand, "phone", command.Phone);
        AddNullable(dbCommand, "email", command.Email);
        AddNullable(dbCommand, "address", command.Address);
        dbCommand.Parameters.AddWithValue("credit_limit_amount", command.CreditLimitAmount);
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            await dbCommand.ExecuteNonQueryAsync(cancellationToken);
            return (await GetCustomerAsync(organizationId, customerId, cancellationToken))!;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<CustomerRecord?> UpdateCustomerAsync(Guid organizationId, Guid customerId, UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            update app.customers
            set display_name = @display_name,
                tax_id = @tax_id,
                phone = @phone,
                email = @email,
                address = @address,
                credit_limit_amount = @credit_limit_amount,
                is_active = @is_active,
                notes = @notes,
                updated_at = timezone('utc', now())
            where organization_id = @organization_id
              and customer_id = @customer_id;
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("customer_id", customerId);
        dbCommand.Parameters.AddWithValue("display_name", command.DisplayName);
        AddNullable(dbCommand, "tax_id", command.TaxId);
        AddNullable(dbCommand, "phone", command.Phone);
        AddNullable(dbCommand, "email", command.Email);
        AddNullable(dbCommand, "address", command.Address);
        dbCommand.Parameters.AddWithValue("credit_limit_amount", command.CreditLimitAmount);
        dbCommand.Parameters.AddWithValue("is_active", command.IsActive);
        AddNullable(dbCommand, "notes", command.Notes);

        try
        {
            var updated = await dbCommand.ExecuteNonQueryAsync(cancellationToken);
            return updated > 0
                ? await GetCustomerAsync(organizationId, customerId, cancellationToken)
                : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ToValidationException(exception);
        }
    }

    public async Task<CustomerAccountMovementListResult> ListAccountMovementsAsync(Guid organizationId, Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cam.customer_account_movement_id,
                cam.customer_id,
                cam.sale_id,
                cam.cash_session_id,
                cam.movement_type,
                cam.concept,
                cam.reference_document,
                cam.debit_amount,
                cam.credit_amount,
                case
                    when cam.movement_type = 'sale_debit'
                    then greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0)
                    else 0
                end as open_amount,
                case
                    when cam.movement_type = 'sale_debit'
                         and cam.due_date is not null
                         and cam.due_date < current_date
                         and greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0) > 0
                    then true
                    else false
                end as is_overdue,
                cam.due_date,
                cam.resulting_balance,
                cam.notes,
                cam.performed_by_user_id,
                cam.created_at,
                count(*) over() as total_count
            from app.customer_account_movements cam
            left join app.customer_payment_allocations cpa
                on cpa.debit_movement_id = cam.customer_account_movement_id
            where cam.organization_id = @organization_id
              and cam.customer_id = @customer_id
            group by cam.customer_account_movement_id
            order by cam.created_at desc, cam.customer_account_movement_id desc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("customer_id", customerId);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("page_size", pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CustomerAccountMovementRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadMovement(reader));
            total = reader.GetInt32(16);
        }

        return new CustomerAccountMovementListResult(items, page, pageSize, total);
    }

    public async Task<CustomerAccountStatementRecord> GetAccountStatementAsync(Guid organizationId, Guid customerId, int limit, CancellationToken cancellationToken)
    {
        var customer = await GetCustomerAsync(organizationId, customerId, cancellationToken)
                       ?? throw new NotFoundException("Cliente no encontrado.");

        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cam.customer_account_movement_id,
                cam.customer_id,
                cam.sale_id,
                cam.cash_session_id,
                cam.movement_type,
                cam.concept,
                cam.reference_document,
                cam.debit_amount,
                cam.credit_amount,
                case
                    when cam.movement_type = 'sale_debit'
                    then greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0)
                    else 0
                end as open_amount,
                case
                    when cam.movement_type = 'sale_debit'
                         and cam.due_date is not null
                         and cam.due_date < current_date
                         and greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0) > 0
                    then true
                    else false
                end as is_overdue,
                cam.due_date,
                cam.resulting_balance,
                cam.notes,
                cam.performed_by_user_id,
                cam.created_at
            from app.customer_account_movements cam
            left join app.customer_payment_allocations cpa
                on cpa.debit_movement_id = cam.customer_account_movement_id
            where cam.organization_id = @organization_id
              and cam.customer_id = @customer_id
            group by cam.customer_account_movement_id
            order by cam.created_at desc, cam.customer_account_movement_id desc
            fetch first @limit rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("customer_id", customerId);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CustomerAccountMovementRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadMovement(reader));
        }

        return new CustomerAccountStatementRecord(customer, items);
    }

    public async Task<RecordCustomerPaymentResult> RecordPaymentAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerPaymentCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.record_customer_payment(
                @organization_id,
                @customer_id,
                @cash_session_id,
                @amount,
                @notes,
                @performed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("customer_id", customerId);
        dbCommand.Parameters.AddWithValue("cash_session_id", command.CashSessionId);
        dbCommand.Parameters.AddWithValue("amount", command.Amount);
        AddNullable(dbCommand, "notes", command.Notes);
        dbCommand.Parameters.AddWithValue("performed_by_user_id", actorUserId);

        try
        {
            var movementId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                     ?? throw new InvalidOperationException("No fue posible registrar la cobranza."));

            var customer = await GetCustomerAsync(organizationId, customerId, cancellationToken)
                           ?? throw new InvalidOperationException("No fue posible recuperar el cliente luego de la cobranza.");

            var movement = await GetMovementAsync(organizationId, movementId, cancellationToken)
                           ?? throw new InvalidOperationException("No fue posible recuperar el movimiento de cuenta corriente.");

            return new RecordCustomerPaymentResult(customer, movement);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<RecordCustomerCreditNoteResult> RecordCreditNoteAsync(Guid organizationId, Guid customerId, Guid actorUserId, RecordCustomerCreditNoteCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.record_customer_credit_note(
                @organization_id,
                @customer_id,
                @amount,
                @concept,
                @reference_document,
                @notes,
                @performed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("customer_id", customerId);
        dbCommand.Parameters.AddWithValue("amount", command.Amount);
        dbCommand.Parameters.AddWithValue("concept", command.Concept);
        AddNullable(dbCommand, "reference_document", command.ReferenceDocument);
        AddNullable(dbCommand, "notes", command.Notes);
        dbCommand.Parameters.AddWithValue("performed_by_user_id", actorUserId);

        try
        {
            var movementId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                     ?? throw new InvalidOperationException("No fue posible registrar la nota de credito."));

            var customer = await GetCustomerAsync(organizationId, customerId, cancellationToken)
                           ?? throw new InvalidOperationException("No fue posible recuperar el cliente luego de la nota de credito.");

            var movement = await GetMovementAsync(organizationId, movementId, cancellationToken)
                           ?? throw new InvalidOperationException("No fue posible recuperar el movimiento de cuenta corriente.");

            return new RecordCustomerCreditNoteResult(customer, movement);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    private async Task<CustomerAccountMovementRecord?> GetMovementAsync(Guid organizationId, Guid movementId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cam.customer_account_movement_id,
                cam.customer_id,
                cam.sale_id,
                cam.cash_session_id,
                cam.movement_type,
                cam.concept,
                cam.reference_document,
                cam.debit_amount,
                cam.credit_amount,
                case
                    when cam.movement_type = 'sale_debit'
                    then greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0)
                    else 0
                end as open_amount,
                case
                    when cam.movement_type = 'sale_debit'
                         and cam.due_date is not null
                         and cam.due_date < current_date
                         and greatest(cam.debit_amount - coalesce(sum(cpa.applied_amount), 0), 0) > 0
                    then true
                    else false
                end as is_overdue,
                cam.due_date,
                cam.resulting_balance,
                cam.notes,
                cam.performed_by_user_id,
                cam.created_at
            from app.customer_account_movements cam
            left join app.customer_payment_allocations cpa
                on cpa.debit_movement_id = cam.customer_account_movement_id
            where cam.organization_id = @organization_id
              and cam.customer_account_movement_id = @movement_id
            group by cam.customer_account_movement_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("movement_id", movementId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMovement(reader)
            : null;
    }

    private static CustomerRecord ReadCustomer(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(12),
            reader.GetDecimal(13),
            reader.IsDBNull(14) ? null : DateOnly.FromDateTime(reader.GetDateTime(14)),
            reader.GetBoolean(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.GetFieldValue<DateTimeOffset>(10),
            reader.GetFieldValue<DateTimeOffset>(11));

    private static CustomerAccountMovementRecord ReadMovement(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9),
            reader.GetBoolean(10),
            reader.IsDBNull(11) ? null : DateOnly.FromDateTime(reader.GetDateTime(11)),
            reader.GetDecimal(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetGuid(14),
            reader.GetFieldValue<DateTimeOffset>(15));

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        NpgsqlParameterHelper.AddNullable(command, name, value);
    }

    private static ValidationException ToValidationException(PostgresException exception)
    {
        if (exception.ConstraintName?.Contains("display_name", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ValidationException("Ya existe un cliente con ese nombre.");
        }

        return new ValidationException("No fue posible persistir la informacion del cliente.");
    }

    private static string ToSpanishMessage(string messageText)
    {
        return messageText switch
        {
            "The informed payment amount must be greater than zero." => "El importe debe ser mayor a cero.",
            "The informed credit note amount must be greater than zero." => "El importe debe ser mayor a cero.",
            "A concept is required to record the credit note." => "Debes informar el concepto de la nota de credito.",
            "The informed customer does not belong to the active organization." => "El cliente informado no pertenece a la organizacion activa.",
            "The informed cash session is not open for the active organization." => "La sesion de caja informada no esta abierta para la organizacion activa.",
            _ => "No fue posible registrar la operacion de cuenta corriente."
        };
    }
}
