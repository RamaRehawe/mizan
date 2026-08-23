namespace Mizan.Models;

/// <summary>A txn that genuinely happened but shouldn't count — a bank reversal, a cancelled
/// purchase. The original row is never deleted; a row here just excludes it from reports.
/// Presence of a row for a txn_id means that txn is void.</summary>
public class TxnVoid : ITimestamped
{
    public int Id { get; set; }
    public int TxnId { get; set; }
    public Txn? Txn { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
