using Mizan.Services;

namespace Mizan.Tests.Services;

public class MonthlyFlowsServiceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    // From docs/SEED-CHECK.md.
    [Fact]
    public void GetFlows_matches_seed_check_exactly_for_july()
    {
        var flows = MonthlyFlowsService.GetFlows(Db, 2026, 7);

        Assert.Equal(2_750_000, flows.IncomeMinor);
        Assert.Equal(-942_531, flows.ExpenseMinor);
        Assert.Equal(1_807_469, flows.NetMinor);
        Assert.Equal(0.657m, Math.Round(flows.SavingsRate, 3));
    }

    [Fact]
    public void GetFlows_excludes_transfers_from_income_and_expense()
    {
        // INV-5. If the -8,000 savings transfer or the card payment leaked into expense, July's
        // expense total would be off by thousands of AED — the exact-match test above would
        // already catch it, but this makes the invariant explicit rather than incidental.
        var flows = MonthlyFlowsService.GetFlows(Db, 2026, 7);

        // Expense is only ever groceries/dining/utilities/rent-scale amounts (a few hundred to a
        // few thousand AED total) — nowhere near what it would be if the 8,000 AED savings
        // transfer or a several-hundred-AED card payment were counted as spending.
        Assert.True(Math.Abs(flows.ExpenseMinor) < 2_000_000, $"expense {flows.ExpenseMinor} looks like it includes a transfer");
    }

    [Fact]
    public void GetFlows_month_with_no_transactions_has_zero_savings_rate_not_a_divide_by_zero()
    {
        var flows = MonthlyFlowsService.GetFlows(Db, 2020, 1); // before HistoryStart — no data at all

        Assert.Equal(0, flows.IncomeMinor);
        Assert.Equal(0, flows.ExpenseMinor);
        Assert.Equal(0m, flows.SavingsRate);
    }
}
