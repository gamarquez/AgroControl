namespace AgroControl.Application.Fiscal;

public sealed record FiscalSettingsRecord(
    Guid OrganizationId,
    string Provider,
    string Environment,
    string TaxpayerId,
    int PointOfSale,
    string ServiceName,
    string DefaultDocumentType,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);

public sealed record FiscalDocumentRecord(
    Guid FiscalDocumentId,
    Guid SaleId,
    long TicketNumber,
    Guid? CustomerId,
    string CustomerName,
    decimal TotalAmount,
    string CurrencyCode,
    string DocumentKind,
    string Provider,
    string Environment,
    string ServiceName,
    string TaxpayerId,
    int PointOfSale,
    string Status,
    long? DocumentNumber,
    string? Cae,
    DateOnly? CaeExpiresOn,
    string? ExternalReference,
    string? LastError,
    int AttemptsCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FiscalDocumentListResult(
    IReadOnlyList<FiscalDocumentRecord> Items,
    int Total);

public sealed record FiscalProbeResult(
    bool IsConfigured,
    bool IsReachable,
    string Provider,
    string Environment,
    string Summary,
    string? TokenExpiresAt,
    string? AuthServer,
    string? AppServer,
    string? DbServer);

public sealed record CreateFiscalDocumentCommand(
    Guid SaleId,
    string DocumentKind);
