namespace AgroControl.Contracts.Fiscal;

public sealed record FiscalSettingsResponse(
    Guid OrganizationId,
    string Provider,
    string Environment,
    string TaxpayerId,
    int PointOfSale,
    string ServiceName,
    string DefaultDocumentType,
    bool IsEnabled,
    DateTimeOffset UpdatedAt);
