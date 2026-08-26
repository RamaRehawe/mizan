using Mizan.Services;

namespace Mizan.Tests.Services;

// Expected values come straight from docs/SEED-CHECK.md — the seed generator's own running
// tally, not a query. Never "greater than zero" here.
public class AccountBalanceServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly AsOf = new(2026, 7, 31);

    [Fact]
    public void GetBalances_returns_every_account()
    {
        var balances = AccountBalanceService.GetBalances(Db, AsOf);

        Assert.Equal(6, balances.Count);
    }

    [Theory]
    [InlineData("Bank - Current", 24_216_491)]
    [InlineData("Bank - Savings", 21_200_000)]
    [InlineData("Broker", 0)]
    [InlineData("Card - Visa", 0)]
    [InlineData("Cash on hand", 308_795)]
    [InlineData("Locker", 0)]
    public void GetBalances_matches_seed_check_exactly(string accountName, long expectedBalanceMinor)
    {
        var balances = AccountBalanceService.GetBalances(Db, AsOf);

        var balance = Assert.Single(balances, b => b.Name == accountName);
        Assert.Equal(expectedBalanceMinor, balance.BalanceMinor);
    }

    [Fact]
    public void GetBalances_before_any_history_equals_opening_balance()
    {
        var dayBeforeHistory = new DateOnly(2024, 7, 31);

        var balances = AccountBalanceService.GetBalances(Db, dayBeforeHistory);

        var current = Assert.Single(balances, b => b.Name == "Bank - Current");
        Assert.Equal(500_000, current.BalanceMinor); // opening balance, no txns yet
    }
}
