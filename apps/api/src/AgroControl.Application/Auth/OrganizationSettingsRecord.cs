namespace AgroControl.Application.Auth;

public sealed record OrganizationSettingsRecord(
    Guid OrganizationId,
    string LegalName,
    string TradeName,
    string TaxId,
    string TimeZone,
    string CurrencyCode);
