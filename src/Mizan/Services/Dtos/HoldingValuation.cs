namespace Mizan.Services.Dtos;

public record HoldingValuation(
    int AssetId, string AssetCode, string AssetName,
    int AccountId, string AccountName,
    decimal Quantity, long PriceMinor, DateOnly PriceAsOf,
    long ValueMinor, string CurrencyCode);
