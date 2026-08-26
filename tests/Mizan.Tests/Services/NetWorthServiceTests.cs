using Mizan.Models;
using Mizan.Services;

namespace Mizan.Tests.Services;

public class NetWorthServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly AsOf = new(2026, 7, 31);

    // From docs/SEED-CHECK.md — July 2026 is a closed period, so this exercises the
    // read-from-snapshot path, not the live one.
    [Fact]
    public void GetNetWorthAt_matches_seed_check_exactly_for_july()
    {
        var result = NetWorthService.GetNetWorthAt(Db, AsOf);

        Assert.Equal(47_599_945, result.TotalMinor);
        Assert.Equal(6, result.ByAccount.Count);
        Assert.Equal(2, result.ByHolding.Count);
    }

    // This is INV-7 itself, not just a description of it: add a price dated after July that's
    // drastically different, then ask for July's net worth again. If this ever failed, it would
    // mean GetNetWorthAt stopped reading the frozen snapshot and started recomputing live for a
    // closed period — exactly the bug snapshots exist to prevent.
    [Fact]
    public void GetNetWorthAt_for_a_closed_period_stays_frozen_even_after_a_later_price_change()
    {
        var before = NetWorthService.GetNetWorthAt(Db, AsOf).TotalMinor;

        var goldAsset = Db.Assets.Single(a => a.Code == "XAU22K");
        var julyPrice = Db.Prices.Single(p => p.AssetId == goldAsset.Id && p.AsOfDate == AsOf).PriceMinor;
        Db.Prices.Add(new Price
        {
            AssetId = goldAsset.Id,
            AsOfDate = new DateOnly(2026, 8, 15),
            Source = PriceSource.Manual,
            PriceMinor = julyPrice * 2, // a deliberately huge jump, impossible to miss if it leaked in
            CurrencyCode = "AED",
        });
        Db.SaveChanges();

        var after = NetWorthService.GetNetWorthAt(Db, AsOf).TotalMinor;

        Assert.Equal(before, after);
    }

    // No period row exists for August 2026 in the base seed, so this takes the live path. Since
    // nothing happens after July 31 in the base seed either, computing live here should land on
    // the exact same number as July's frozen snapshot — the two code paths agreeing when there's
    // genuinely nothing for them to disagree about.
    [Fact]
    public void GetNetWorthAt_for_a_date_with_no_period_computes_live_and_agrees_with_the_frozen_number()
    {
        var julyFrozen = NetWorthService.GetNetWorthAt(Db, AsOf).TotalMinor;

        var live = NetWorthService.GetNetWorthAt(Db, new DateOnly(2026, 8, 15));

        Assert.Equal(julyFrozen, live.TotalMinor);
    }
}
