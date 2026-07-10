namespace AgroControl.Contracts.Settings;

public sealed record OrganizationSettingsResponse(
    Guid OrganizationId,
    string LegalName,
    string TradeName,
    string TaxId,
    string TimeZone,
    string CurrencyCode);
