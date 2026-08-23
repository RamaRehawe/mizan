namespace Mizan.Models;

/// <summary>One row per parent -> child link when a txn is split into pieces. The parent row is
/// retained but excluded from reports (a txn is a split parent if it appears as ParentTxnId
/// here); the children carry the real categorization.</summary>
public class TxnSplit : ITimestamped
{
    public int Id { get; set; }
    public int ParentTxnId { get; set; }
    public Txn? ParentTxn { get; set; }
    public int ChildTxnId { get; set; }
    public Txn? ChildTxn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
