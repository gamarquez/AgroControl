namespace AgroControl.Contracts.Fiscal;

public sealed record UpdateFiscalSettingsRequest(
    string Provider,
    string Environment,
    string TaxpayerId,
    int PointOfSale,
    string ServiceName,
    string DefaultDocumentType,
    bool IsEnabled);
