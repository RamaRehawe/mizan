namespace Mizan.Models;

/// <summary>One row per buy/sell/gift. Current quantity is always SUM(qty_delta) for a holding
/// — never a stored column (INV-6).</summary>
public class HoldingTxn : ITimestamped
{
    public int Id { get; set; }
    public int HoldingId { get; set; }
    public Holding? Holding { get; set; }
    public DateOnly OccurredOn { get; set; }
    public decimal QtyDelta { get; set; }
    public long? UnitCostMinor { get; set; }
    public long FeeMinor { get; set; }
    public required string CurrencyCode { get; set; }

    // The cash-side transaction, if this movement paid for or was paid by one — e.g. a gold
    // purchase debits Bank - Current and this points at that txn.
    public int? LinkedTxnId { get; set; }
    public Txn? LinkedTxn { get; set; }

    public HoldingTxnOrigin Origin { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
