using AgroControl.Application.Fiscal;
using AgroControl.Application.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace AgroControl.Infrastructure.Persistence;

internal sealed class NpgsqlFiscalRepository(ISqlConnectionFactory connectionFactory) : IFiscalRepository
{
    public async Task<FiscalSettingsRecord?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                organization_id,
                provider,
                environment,
                taxpayer_id,
                point_of_sale,
                service_name,
                default_document_type,
                is_enabled,
                updated_at
            from app.fiscal_settings
            where organization_id = @organization_id;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new FiscalSettingsRecord(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    public async Task<FiscalSettingsRecord> UpsertSettingsAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            insert into app.fiscal_settings (
                organization_id,
                provider,
                environment,
                taxpayer_id,
                point_of_sale,
                service_name,
                default_document_type,
                is_enabled,
                updated_at
            )
            values (
                @organization_id,
                @provider,
                @environment,
                @taxpayer_id,
                @point_of_sale,
                @service_name,
                @default_document_type,
                @is_enabled,
                timezone('utc', now())
            )
            on conflict (organization_id) do update
            set provider = excluded.provider,
                environment = excluded.environment,
                taxpayer_id = excluded.taxpayer_id,
                point_of_sale = excluded.point_of_sale,
                service_name = excluded.service_name,
                default_document_type = excluded.default_document_type,
                is_enabled = excluded.is_enabled,
                updated_at = timezone('utc', now());
            """;
        command.Parameters.AddWithValue("organization_id", settings.OrganizationId);
        command.Parameters.AddWithValue("provider", settings.Provider);
        command.Parameters.AddWithValue("environment", settings.Environment);
        command.Parameters.AddWithValue("taxpayer_id", settings.TaxpayerId);
        command.Parameters.AddWithValue("point_of_sale", settings.PointOfSale);
        command.Parameters.AddWithValue("service_name", settings.ServiceName);
        command.Parameters.AddWithValue("default_document_type", settings.DefaultDocumentType);
        command.Parameters.AddWithValue("is_enabled", settings.IsEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return (await GetSettingsAsync(settings.OrganizationId, cancellationToken))!;
    }

    public async Task<FiscalDocumentListResult> ListDocumentsAsync(Guid organizationId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                fd.fiscal_document_id,
                fd.sale_id,
                s.ticket_number,
                s.customer_id,
                s.customer_name,
                s.total_amount,
                s.currency_code,
                fd.document_kind,
                fd.provider,
                fd.environment,
                fd.service_name,
                fd.taxpayer_id,
                fd.point_of_sale,
                fd.status,
                fd.document_number,
                fd.cae,
                fd.cae_expires_on,
                fd.external_reference,
                fd.last_error,
                fd.attempts_count,
                fd.last_attempt_at,
                fd.created_at,
                fd.updated_at,
                count(*) over() as total_count
            from app.fiscal_documents fd
            join app.sales s on s.sale_id = fd.sale_id
            where fd.organization_id = @organization_id
            order by fd.created_at desc
            limit @limit;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<FiscalDocumentRecord>();
        var total = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadDocument(reader));
            total = reader.GetInt32(23);
        }

        return new FiscalDocumentListResult(items, total);
    }

    public async Task<FiscalDocumentRecord?> GetDocumentBySaleAsync(Guid organizationId, Guid saleId, string documentKind, CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            select
                fd.fiscal_document_id,
                fd.sale_id,
                s.ticket_number,
                s.customer_id,
                s.customer_name,
                s.total_amount,
                s.currency_code,
                fd.document_kind,
                fd.provider,
                fd.environment,
                fd.service_name,
                fd.taxpayer_id,
                fd.point_of_sale,
                fd.status,
                fd.document_number,
                fd.cae,
                fd.cae_expires_on,
                fd.external_reference,
                fd.last_error,
                fd.attempts_count,
                fd.last_attempt_at,
                fd.created_at,
                fd.updated_at
            from app.fiscal_documents fd
            join app.sales s on s.sale_id = fd.sale_id
            where fd.organization_id = @organization_id
              and fd.sale_id = @sale_id
              and fd.document_kind = @document_kind;
            """;
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("sale_id", saleId);
        command.Parameters.AddWithValue("document_kind", documentKind);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadDocument(reader)
            : null;
    }

    public async Task<FiscalDocumentRecord> CreatePendingDocumentAsync(
        Guid organizationId,
        SaleDocumentSnapshot sale,
        FiscalSettingsRecord settings,
        string documentKind,
        string? lastError,
        string requestPayload,
        CancellationToken cancellationToken)
    {
        var fiscalDocumentId = Guid.NewGuid();

        await using var connection = (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                insert into app.fiscal_documents (
                    fiscal_document_id,
                    organization_id,
                    sale_id,
                    document_kind,
                    provider,
                    environment,
                    service_name,
                    taxpayer_id,
                    point_of_sale,
                    status,
                    document_number,
                    cae,
                    cae_expires_on,
                    external_reference,
                    last_error,
                    attempts_count,
                    last_attempt_at,
                    created_at,
                    updated_at
                )
                values (
                    @fiscal_document_id,
                    @organization_id,
                    @sale_id,
                    @document_kind,
                    @provider,
                    @environment,
                    @service_name,
                    @taxpayer_id,
                    @point_of_sale,
                    'pending',
                    null,
                    null,
                    null,
                    @external_reference,
                    @last_error,
                    1,
                    timezone('utc', now()),
                    timezone('utc', now()),
                    timezone('utc', now())
                );
                """;
            command.Parameters.AddWithValue("fiscal_document_id", fiscalDocumentId);
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("sale_id", sale.SaleId);
            command.Parameters.AddWithValue("document_kind", documentKind);
            command.Parameters.AddWithValue("provider", settings.Provider);
            command.Parameters.AddWithValue("environment", settings.Environment);
            command.Parameters.AddWithValue("service_name", settings.ServiceName);
            command.Parameters.AddWithValue("taxpayer_id", settings.TaxpayerId);
            command.Parameters.AddWithValue("point_of_sale", settings.PointOfSale);
            command.Parameters.AddWithValue("external_reference", $"SALE-{sale.TicketNumber:D8}");
            AddNullable(command, "last_error", lastError);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await WriteRequestLogAsync(
            organizationId,
            fiscalDocumentId,
            "queue",
            requestPayload,
            null,
            false,
            lastError,
            cancellationToken,
            connection,
            transaction);

        await transaction.CommitAsync(cancellationToken);

        return (await GetDocumentBySaleAsync(organizationId, sale.SaleId, documentKind, cancellationToken))!;
    }

    public Task WriteRequestLogAsync(
        Guid organizationId,
        Guid? fiscalDocumentId,
        string requestKind,
        string? requestPayload,
        string? responsePayload,
        bool isSuccess,
        string? errorMessage,
        CancellationToken cancellationToken)
        => WriteRequestLogAsync(
            organizationId,
            fiscalDocumentId,
            requestKind,
            requestPayload,
            responsePayload,
            isSuccess,
            errorMessage,
            cancellationToken,
            connection: null,
            transaction: null);

    private static FiscalDocumentRecord ReadDocument(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetInt64(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetString(4),
            reader.GetDecimal(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetInt32(12),
            reader.GetString(13),
            reader.IsDBNull(14) ? null : reader.GetInt64(14),
            reader.IsDBNull(15) ? null : reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetFieldValue<DateOnly>(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.GetInt32(19),
            reader.IsDBNull(20) ? null : reader.GetFieldValue<DateTimeOffset>(20),
            reader.GetFieldValue<DateTimeOffset>(21),
            reader.GetFieldValue<DateTimeOffset>(22));

    private async Task WriteRequestLogAsync(
        Guid organizationId,
        Guid? fiscalDocumentId,
        string requestKind,
        string? requestPayload,
        string? responsePayload,
        bool isSuccess,
        string? errorMessage,
        CancellationToken cancellationToken,
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction)
    {
        var ownsConnection = connection is null;
        await using var localConnection = ownsConnection
            ? (NpgsqlConnection)await connectionFactory.OpenConnectionAsync(cancellationToken)
            : null;

        var activeConnection = connection ?? localConnection!;
        await using var command = activeConnection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            insert into app.fiscal_request_logs (
                fiscal_request_log_id,
                organization_id,
                fiscal_document_id,
                request_kind,
                request_payload,
                response_payload,
                is_success,
                error_message,
                created_at
            )
            values (
                @fiscal_request_log_id,
                @organization_id,
                @fiscal_document_id,
                @request_kind,
                @request_payload,
                @response_payload,
                @is_success,
                @error_message,
                timezone('utc', now())
            );
            """;
        command.Parameters.AddWithValue("fiscal_request_log_id", Guid.NewGuid());
        command.Parameters.AddWithValue("organization_id", organizationId);
        AddNullable(command, "fiscal_document_id", fiscalDocumentId);
        command.Parameters.AddWithValue("request_kind", requestKind);
        AddJsonNullable(command, "request_payload", requestPayload);
        AddJsonNullable(command, "response_payload", responsePayload);
        command.Parameters.AddWithValue("is_success", isSuccess);
        AddNullable(command, "error_message", errorMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable<T>(NpgsqlCommand command, string name, T? value)
    {
        NpgsqlParameterHelper.AddNullable(command, name, value);
    }

    private static void AddJsonNullable(NpgsqlCommand command, string name, string? json)
    {
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb)
        {
            Value = json is null ? DBNull.Value : json
        });
    }
}
