namespace Mizan.Models;

/// <summary>Records a correction: amount, date, or account can never be UPDATEd on a txn in
/// place, so fixing one means inserting a new row and linking old -> new here. The old row is
/// never touched. A txn is "current" if it never appears as OldTxnId in this table.</summary>
public class TxnSupersession : ITimestamped
{
    public int Id { get; set; }
    public int OldTxnId { get; set; }
    public Txn? OldTxn { get; set; }
    public int NewTxnId { get; set; }
    public Txn? NewTxn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
