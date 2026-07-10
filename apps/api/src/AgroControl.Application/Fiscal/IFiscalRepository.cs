namespace AgroControl.Application.Fiscal;

public interface IFiscalRepository
{
    Task<FiscalSettingsRecord?> GetSettingsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<FiscalSettingsRecord> UpsertSettingsAsync(FiscalSettingsRecord settings, CancellationToken cancellationToken);

    Task<FiscalDocumentListResult> ListDocumentsAsync(Guid organizationId, int limit, CancellationToken cancellationToken);

    Task<FiscalDocumentRecord?> GetDocumentBySaleAsync(Guid organizationId, Guid saleId, string documentKind, CancellationToken cancellationToken);

    Task<FiscalDocumentRecord> CreatePendingDocumentAsync(
        Guid organizationId,
        SaleDocumentSnapshot sale,
        FiscalSettingsRecord settings,
        string documentKind,
        string? lastError,
        string requestPayload,
        CancellationToken cancellationToken);

    Task WriteRequestLogAsync(
        Guid organizationId,
        Guid? fiscalDocumentId,
        string requestKind,
        string? requestPayload,
        string? responsePayload,
        bool isSuccess,
        string? errorMessage,
        CancellationToken cancellationToken);
}

public sealed record SaleDocumentSnapshot(
    Guid SaleId,
    long TicketNumber,
    Guid? CustomerId,
    string CustomerName,
    decimal TotalAmount,
    string CurrencyCode,
    string Status);
