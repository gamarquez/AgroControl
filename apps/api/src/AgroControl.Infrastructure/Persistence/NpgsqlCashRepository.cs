using AgroControl.Application.Auth;
using AgroControl.Application.Cash;
using AgroControl.Application.Persistence;
using Npgsql;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlCashRepository(ISqlConnectionFactory connectionFactory) : ICashRepository
{
    public async Task<CashOverviewRecord> GetOverviewAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var cashRegister = await GetPrimaryCashRegisterAsync(organizationId, cancellationToken)
                           ?? throw new NotFoundException("No se encontro una caja activa para la organizacion.");

        var currentSession = await GetCurrentSessionAsync(organizationId, cashRegister.CashRegisterId, cancellationToken);
        var recentMovements = await ListRecentMovementsAsync(organizationId, 20, cancellationToken);

        return new CashOverviewRecord(cashRegister, currentSession, recentMovements);
    }

    public async Task<CashMovementListResult> ListMovementsAsync(Guid organizationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cm.cash_movement_id,
                cm.cash_session_id,
                cm.movement_type,
                cm.category_code,
                cm.concept,
                cm.payment_method,
                cm.amount,
                cm.signed_amount,
                cm.resulting_balance,
                cm.reference_document,
                cm.notes,
                cm.performed_by_user_id,
                cm.created_at,
                count(*) over() as total_count
            from app.cash_movements cm
            where cm.organization_id = @organization_id
            order by cm.created_at desc
            offset @offset rows fetch next @page_size rows only;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
        command.Parameters.AddWithValue("page_size", pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CashMovementRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadMovement(reader));
            total = reader.GetInt32(13);
        }

        return new CashMovementListResult(items, page, pageSize, total);
    }

    public async Task<CashSessionRecord?> GetSessionAsync(Guid organizationId, Guid cashSessionId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SessionSelectSql + "\n and cs.cash_session_id = @cash_session_id;";
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("cash_session_id", cashSessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSession(reader)
            : null;
    }

    public async Task<CashSessionRecord> OpenSessionAsync(Guid organizationId, Guid actorUserId, OpenCashSessionCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.open_cash_session(
                @organization_id,
                @cash_register_code,
                @opening_amount,
                @opening_notes,
                @opened_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("cash_register_code", command.CashRegisterCode);
        dbCommand.Parameters.AddWithValue("opening_amount", command.OpeningAmount);
        AddNullable(dbCommand, "opening_notes", command.OpeningNotes);
        dbCommand.Parameters.AddWithValue("opened_by_user_id", actorUserId);

        try
        {
            var sessionId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                   ?? throw new InvalidOperationException("No fue posible abrir la sesion de caja."));

            return await GetSessionAsync(organizationId, sessionId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la sesion de caja creada.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<CashMovementRecord> CreateMovementAsync(Guid organizationId, Guid actorUserId, CreateCashMovementCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.record_cash_movement(
                @organization_id,
                @cash_session_id,
                @movement_type,
                @category_code,
                @concept,
                @payment_method,
                @amount,
                @reference_document,
                @notes,
                @performed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("cash_session_id", command.CashSessionId);
        dbCommand.Parameters.AddWithValue("movement_type", command.MovementType);
        dbCommand.Parameters.AddWithValue("category_code", command.CategoryCode);
        dbCommand.Parameters.AddWithValue("concept", command.Concept);
        dbCommand.Parameters.AddWithValue("payment_method", command.PaymentMethod);
        dbCommand.Parameters.AddWithValue("amount", command.Amount);
        AddNullable(dbCommand, "reference_document", command.ReferenceDocument);
        AddNullable(dbCommand, "notes", command.Notes);
        dbCommand.Parameters.AddWithValue("performed_by_user_id", actorUserId);

        try
        {
            var movementId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                    ?? throw new InvalidOperationException("No fue posible registrar el movimiento de caja."));

            return await GetMovementAsync(organizationId, movementId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar el movimiento de caja creado.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    public async Task<CashSessionRecord> CloseSessionAsync(Guid organizationId, Guid actorUserId, Guid cashSessionId, CloseCashSessionCommand command, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText =
            """
            select app.close_cash_session(
                @organization_id,
                @cash_session_id,
                @closing_amount,
                @closing_notes,
                @closed_by_user_id);
            """;
        dbCommand.Parameters.AddWithValue("organization_id", organizationId);
        dbCommand.Parameters.AddWithValue("cash_session_id", cashSessionId);
        dbCommand.Parameters.AddWithValue("closing_amount", command.ClosingAmount);
        AddNullable(dbCommand, "closing_notes", command.ClosingNotes);
        dbCommand.Parameters.AddWithValue("closed_by_user_id", actorUserId);

        try
        {
            var sessionId = (Guid)(await dbCommand.ExecuteScalarAsync(cancellationToken)
                                   ?? throw new InvalidOperationException("No fue posible cerrar la sesion de caja."));

            return await GetSessionAsync(organizationId, sessionId, cancellationToken)
                   ?? throw new InvalidOperationException("No fue posible recuperar la sesion de caja cerrada.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.RaiseException)
        {
            throw new ValidationException(ToSpanishMessage(exception.MessageText));
        }
    }

    private async Task<CashRegisterRecord?> GetPrimaryCashRegisterAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cr.cash_register_id,
                cr.organization_id,
                cr.name,
                cr.code,
                cr.is_active
            from app.cash_registers cr
            where cr.organization_id = @organization_id
              and cr.is_active = true
            order by case when cr.code = 'main' then 0 else 1 end, cr.name asc
            limit 1;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadRegister(reader)
            : null;
    }

    private async Task<CashSessionRecord?> GetCurrentSessionAsync(Guid organizationId, Guid cashRegisterId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SessionSelectSql + "\n and cs.cash_register_id = @cash_register_id and cs.status = 'open' order by cs.opened_at desc limit 1;";
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("cash_register_id", cashRegisterId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSession(reader)
            : null;
    }

    private async Task<IReadOnlyList<CashMovementRecord>> ListRecentMovementsAsync(Guid organizationId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cm.cash_movement_id,
                cm.cash_session_id,
                cm.movement_type,
                cm.category_code,
                cm.concept,
                cm.payment_method,
                cm.amount,
                cm.signed_amount,
                cm.resulting_balance,
                cm.reference_document,
                cm.notes,
                cm.performed_by_user_id,
                cm.created_at
            from app.cash_movements cm
            where cm.organization_id = @organization_id
            order by cm.created_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<CashMovementRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadMovement(reader));
        }

        return items;
    }

    private async Task<CashMovementRecord?> GetMovementAsync(Guid organizationId, Guid movementId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                cm.cash_movement_id,
                cm.cash_session_id,
                cm.movement_type,
                cm.category_code,
                cm.concept,
                cm.payment_method,
                cm.amount,
                cm.signed_amount,
                cm.resulting_balance,
                cm.reference_document,
                cm.notes,
                cm.performed_by_user_id,
                cm.created_at
            from app.cash_movements cm
            where cm.organization_id = @organization_id
              and cm.cash_movement_id = @cash_movement_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("cash_movement_id", movementId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMovement(reader)
            : null;
    }

    private static CashRegisterRecord ReadRegister(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4));

    private static CashSessionRecord ReadSession(NpgsqlDataReader reader)
    {
        var cashRegister = new CashRegisterRecord(
            reader.GetGuid(2),
            reader.GetGuid(1),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5));

        return new CashSessionRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            cashRegister,
            reader.GetGuid(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetDecimal(8),
            reader.IsDBNull(9) ? null : reader.GetDecimal(9),
            reader.IsDBNull(10) ? null : reader.GetDecimal(10),
            reader.GetDecimal(11),
            reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.GetFieldValue<DateTimeOffset>(15),
            reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16));
    }

    private static CashMovementRecord ReadMovement(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetGuid(11),
            reader.GetFieldValue<DateTimeOffset>(12));

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value);
    }

    private static string ToSpanishMessage(string messageText)
    {
        return messageText switch
        {
            "The opening amount cannot be negative." => "El monto de apertura no puede ser negativo.",
            "The informed cash register is not available for the active organization." => "La caja informada no esta disponible para la organizacion activa.",
            "There is already an open cash session for the informed register." => "Ya existe una sesion de caja abierta para la caja informada.",
            "The informed cash amount must be greater than zero." => "El importe debe ser mayor a cero.",
            "The informed cash movement type is not supported." => "El tipo de movimiento informado no es valido.",
            "The informed cash session is not open for the active organization." => "La sesion de caja informada no esta abierta para la organizacion activa.",
            "There is not enough balance to register the requested cash out movement." => "No hay saldo suficiente para registrar el egreso solicitado.",
            "The closing amount cannot be negative." => "El monto de cierre no puede ser negativo.",
            _ => "No fue posible persistir la informacion de caja informada."
        };
    }

    private const string SessionSelectSql =
        """
        select
            cs.cash_session_id,
            cs.organization_id,
            cr.cash_register_id,
            cr.name,
            cr.code,
            cr.is_active,
            cs.opened_by_user_id,
            cs.closed_by_user_id,
            cs.opening_amount,
            cs.closing_amount,
            cs.difference_amount,
            cs.current_balance,
            cs.status,
            cs.opening_notes,
            cs.closing_notes,
            cs.opened_at,
            cs.closed_at
        from app.cash_sessions cs
        join app.cash_registers cr on cr.cash_register_id = cs.cash_register_id
        where cs.organization_id = @organization_id
        """;
}
