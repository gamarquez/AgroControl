namespace AgroControl.Contracts.Settings;

public sealed record UpdateOrganizationSettingsRequest(
    string LegalName,
    string TradeName,
    string TaxId,
    string TimeZone,
    string CurrencyCode);
