using Microsoft.EntityFrameworkCore;
using Mizan.Models;
using Mizan.Services.Dtos;

namespace Mizan.Services;

public static class NetWorthService
{
    /// <summary>Net worth as of the given date. If that date falls inside a closed period, this
    /// reads the frozen snapshot instead of computing anything — INV-7: a number for a closed
    /// month must never change just because a price changed later. Only an open (or
    /// nonexistent) period gets computed live, from current balances and holding valuations.</summary>
    public static NetWorthResult GetNetWorthAt(MizanDbContext db, DateOnly asOf)
    {
        var period = db.Periods.SingleOrDefault(p => p.Year == asOf.Year && p.Month == asOf.Month);

        if (period is { Status: PeriodStatus.Closed })
        {
            return ReadFromSnapshot(db, period, asOf);
        }

        var balances = AccountBalanceService.GetBalances(db, asOf);
        var holdings = HoldingValuationService.GetValuations(db, asOf);
        var totalMinor = balances.Sum(b => b.BalanceMinor) + holdings.Sum(h => h.ValueMinor);

        return new NetWorthResult(asOf, totalMinor, balances, holdings);
    }

    private static NetWorthResult ReadFromSnapshot(MizanDbContext db, Period period, DateOnly asOf)
    {
        // Most recent snapshot for this period, in case a restatement (FR-9.5) ever exists —
        // nothing creates a second one yet, so today this is always exactly one row.
        var snapshot = db.Snapshots
            .Where(s => s.PeriodId == period.Id)
            .OrderByDescending(s => s.TakenAt)
            .First();

        var lines = db.SnapshotLines
            .Include(l => l.Account)
            .Include(l => l.Asset)
            .Where(l => l.SnapshotId == snapshot.Id)
            .ToList();

        var byAccount = lines
            .Where(l => l.AssetId == null)
            .Select(l => new AccountBalance(l.Account!.Id, l.Account.Name, l.Account.LiquidityClass, l.Account.CurrencyCode, l.BalanceMinor))
            .ToList();

        // snapshot_line has no currency column of its own — BalanceMinor there is already in
        // base currency by construction (that's what BalanceBaseMinor vs. BalanceMinor is for),
        // and v0.1's base currency is AED throughout. Not a hack, just what "base currency" means
        // until FX exists.
        var byHolding = lines
            .Where(l => l.AssetId != null)
            .Select(l => new HoldingValuation(
                l.Asset!.Id, l.Asset.Code, l.Asset.Name, l.Account!.Id, l.Account.Name,
                l.Quantity!.Value, l.PriceMinor!.Value, l.PriceAsOf!.Value, l.BalanceMinor, "AED"))
            .ToList();

        return new NetWorthResult(asOf, snapshot.TotalNetWorthMinor, byAccount, byHolding);
    }
}
