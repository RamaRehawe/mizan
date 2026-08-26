namespace Mizan.Models;

/// <summary>The itemized detail behind a Snapshot's total. AccountId set with AssetId null is a
/// plain cash balance line; both set is a holding valuation line (quantity, the price used, and
/// that price's own date, so a stale price stays visible even inside a frozen snapshot).
/// SUM(BalanceMinor) across a snapshot's lines equals Snapshot.TotalNetWorthMinor.</summary>
public class SnapshotLine : ITimestamped
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public Snapshot? Snapshot { get; set; }

    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public decimal? Quantity { get; set; }
    public long? PriceMinor { get; set; }
    public DateOnly? PriceAsOf { get; set; }

    public long BalanceMinor { get; set; }

    // Equal to BalanceMinor for now — v0.1 is AED-only. Kept ready for when FX exists rather
    // than added as a breaking change later.
    public long BalanceBaseMinor { get; set; }
    public decimal? FxRateUsed { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
