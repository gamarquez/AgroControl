namespace AgroControl.Contracts.Catalog;

public sealed record ProductPriceResponse(
    Guid PriceListId,
    string PriceListName,
    string PriceListCode,
    decimal CostAmount,
    decimal? MarginPercent,
    decimal SaleAmount,
    string CurrencyCode,
    DateTimeOffset EffectiveFrom);
