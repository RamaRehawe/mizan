namespace Mizan.Models;

public class Asset : ITimestamped
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public AssetClass AssetClass { get; set; }
    public AssetUnit Unit { get; set; }

    // Only meaningful when AssetClass is Gold.
    public GoldPurity? Purity { get; set; }

    public required string QuoteCurrencyCode { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
