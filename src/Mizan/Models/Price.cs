namespace Mizan.Models;

/// <summary>A dated unit price for an asset. No surrogate id — the primary key is
/// (AssetId, AsOfDate, Source) directly, matching REQUIREMENTS.md §5: the same asset can have
/// both a manual and a fetched price on the same day, and both are kept.</summary>
public class Price : ITimestamped
{
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }
    public DateOnly AsOfDate { get; set; }
    public PriceSource Source { get; set; }
    public long PriceMinor { get; set; }
    public required string CurrencyCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
