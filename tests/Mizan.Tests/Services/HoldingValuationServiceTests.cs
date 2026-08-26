using Mizan.Services;

namespace Mizan.Tests.Services;

// Expected values come straight from docs/SEED-CHECK.md's Holdings section — the seed
// generator's own running tally, not a query.
public class HoldingValuationServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly AsOf = new(2026, 7, 31);

    [Theory]
    [InlineData("XAU22K", 13.94, 31_717L, 442_135L)]
    [InlineData("GEF", 114.0, 12_566L, 1_432_524L)]
    public void GetValuations_matches_seed_check_exactly(string assetCode, decimal expectedQuantity, long expectedPriceMinor, long expectedValueMinor)
    {
        var valuations = HoldingValuationService.GetValuations(Db, AsOf);

        var v = Assert.Single(valuations, h => h.AssetCode == assetCode);
        Assert.Equal(expectedQuantity, v.Quantity);
        Assert.Equal(expectedPriceMinor, v.PriceMinor);
        Assert.Equal(expectedValueMinor, v.ValueMinor);
    }

    [Fact]
    public void GetValuations_returns_both_seeded_holdings()
    {
        var valuations = HoldingValuationService.GetValuations(Db, AsOf);

        Assert.Equal(2, valuations.Count);
    }

    [Fact]
    public void GetValuations_before_any_history_returns_nothing()
    {
        // No purchases and no prices exist before HistoryStart — a holding with no price yet is
        // skipped, not an error (a real, valid state), so the list is simply empty.
        var dayBeforeHistory = new DateOnly(2024, 7, 31);

        var valuations = HoldingValuationService.GetValuations(Db, dayBeforeHistory);

        Assert.Empty(valuations);
    }
}
