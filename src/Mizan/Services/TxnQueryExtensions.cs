using Mizan.Models;

namespace Mizan.Services;

public static class TxnQueryExtensions
{
    /// <summary>Not void and not superseded by a later correction — INV-3's notion of "live".
    /// Nothing writes to TxnVoid/TxnSupersession yet in v0.1 (no correction UI exists), so
    /// today this is a no-op filter — but getting it right now means the query never needs a
    /// second look once that feature exists.</summary>
    public static IQueryable<Txn> LiveTxns(this MizanDbContext db) =>
        db.Txns.Where(t =>
            !db.TxnVoids.Any(v => v.TxnId == t.Id) &&
            !db.TxnSupersessions.Any(s => s.OldTxnId == t.Id));

    /// <summary>INV-5: both legs of a transfer are excluded from income and expense. Written
    /// once, used only by MonthlyFlowsService — AccountBalanceService must never apply this,
    /// since a transfer still has to move both account balances.</summary>
    public static IQueryable<Txn> ExcludingTransfers(this IQueryable<Txn> txns) =>
        txns.Where(t => t.Category != null &&
            (t.Category.Kind == CategoryKind.Income || t.Category.Kind == CategoryKind.Expense));
}
