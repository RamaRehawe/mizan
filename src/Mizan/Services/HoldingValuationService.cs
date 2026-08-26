using Microsoft.EntityFrameworkCore;
using Mizan.Models;
using Mizan.Services.Dtos;

namespace Mizan.Services;

public static class HoldingValuationService
{
    /// <summary>Value of every holding as of the given date: quantity — always SUM(qty_delta)
    /// through that date, never a stored total (INV-6) — times the latest price at or before
    /// that date.
    ///
    /// A holding's price is used exactly as entered for that specific asset. An asset's purity
    /// (if any) is descriptive, not a multiplier here: a "Gold 22k" asset's price is expected to
    /// already be a 22k price, not a 24k reference needing further scaling. FR-8.6's
    /// purity-factor formula only matters if you're deriving a lower-purity value from a shared
    /// 24k reference price — this system doesn't do that; manual entry per asset is always
    /// sufficient (FR-8.4), so whoever enters the price enters the right one for that asset.
    ///
    /// A holding with no price on or before asOf yet is skipped, not an error — that's a real,
    /// valid state (a holding can exist before its first price does).</summary>
    public static IReadOnlyList<HoldingValuation> GetValuations(MizanDbContext db, DateOnly asOf)
    {
        var holdings = db.Holdings.Include(h => h.Account).Include(h => h.Asset).ToList();
        var result = new List<HoldingValuation>(holdings.Count);

        foreach (var holding in holdings)
        {
            var asset = holding.Asset!;
            var account = holding.Account!;

            // Summed in memory, not via SQL SUM() — qty_delta is stored as exact decimal text
            // (never SQLite's float-backed NUMERIC), and SQLite's SUM() would coerce it through
            // floating point on the way out.
            var quantity = db.HoldingTxns
                .Where(t => t.HoldingId == holding.Id && t.OccurredOn <= asOf)
                .Select(t => t.QtyDelta)
                .ToList()
                .Sum();

            var price = db.Prices
                .Where(p => p.AssetId == asset.Id && p.AsOfDate <= asOf)
                .OrderByDescending(p => p.AsOfDate)
                .FirstOrDefault();

            if (price == null)
            {
                continue;
            }

            var valueMinor = (long)Math.Round(quantity * price.PriceMinor);

            result.Add(new HoldingValuation(
                asset.Id, asset.Code, asset.Name, account.Id, account.Name,
                quantity, price.PriceMinor, price.AsOfDate, valueMinor, price.CurrencyCode));
        }

        return result;
    }
}
