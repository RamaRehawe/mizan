using Microsoft.EntityFrameworkCore;
using Mizan.Models;
using Mizan.Services;

namespace Mizan.Tests.Invariants;

public class InvariantTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public void INV3_dedupe_key_is_unique_across_live_txns()
    {
        var existing = Db.Txns.First();

        var duplicate = new Txn
        {
            AccountId = existing.AccountId,
            OccurredOn = existing.OccurredOn,
            AmountMinor = existing.AmountMinor,
            CurrencyCode = existing.CurrencyCode,
            DescriptionRaw = "duplicate",
            Origin = TxnOrigin.Manual,
            DedupeKey = existing.DedupeKey,
        };
        Db.Txns.Add(duplicate);

        Assert.Throws<DbUpdateException>(() => Db.SaveChanges());
    }

    [Fact]
    public void INV3_dedupe_key_stays_unique_even_after_the_original_is_voided()
    {
        // Per review: a dedupe_key must never be reusable, void or not — occurrence_index
        // already exists to distinguish genuinely repeated real transactions, so there's no
        // legitimate reason a new live txn would need to reuse a dead row's exact key.
        var existing = Db.Txns.First();
        Db.TxnVoids.Add(new TxnVoid { TxnId = existing.Id, Reason = "test" });
        Db.SaveChanges();

        var duplicate = new Txn
        {
            AccountId = existing.AccountId,
            OccurredOn = existing.OccurredOn,
            AmountMinor = existing.AmountMinor,
            CurrencyCode = existing.CurrencyCode,
            DescriptionRaw = "duplicate after void",
            Origin = TxnOrigin.Manual,
            DedupeKey = existing.DedupeKey,
        };
        Db.Txns.Add(duplicate);

        Assert.Throws<DbUpdateException>(() => Db.SaveChanges());
    }

    [Fact]
    public void INV5_transfer_legs_move_balances_but_never_count_as_income_or_expense()
    {
        var transferCategory = Db.Categories.Single(c => c.Name == "Savings Transfer");
        var asOfBefore = new DateOnly(2026, 6, 30);
        var asOfAfter = new DateOnly(2026, 7, 31);

        var balanceBefore = AccountBalanceService.GetBalances(Db, asOfBefore).Single(b => b.Name == "Bank - Current").BalanceMinor;
        var balanceAfter = AccountBalanceService.GetBalances(Db, asOfAfter).Single(b => b.Name == "Bank - Current").BalanceMinor;
        var flows = MonthlyFlowsService.GetFlows(Db, 2026, 7);

        var julyTransferTxns = Db.Txns.Count(t => t.CategoryId == transferCategory.Id
            && t.OccurredOn.Year == 2026 && t.OccurredOn.Month == 7);
        Assert.True(julyTransferTxns > 0, "sanity check: the seed data should contain a July transfer");

        // Current's balance movement includes the transfer, the card payment, and the ATM
        // withdrawal; the flows net deliberately excludes all three. If ExcludingTransfers()
        // ever broke, these two numbers would converge — they must not.
        var balanceChange = balanceAfter - balanceBefore;
        Assert.NotEqual(flows.NetMinor, balanceChange);
        Assert.Equal(2_750_000, flows.IncomeMinor); // salary only — the transfer never enters this sum
    }

    [Fact]
    public void INV8_mixed_currency_on_one_account_is_rejected_not_silently_summed()
    {
        var account = Db.Accounts.Single(a => a.Name == "Bank - Current");
        Db.Txns.Add(new Txn
        {
            AccountId = account.Id,
            OccurredOn = new DateOnly(2026, 7, 15),
            AmountMinor = 1000,
            CurrencyCode = "USD", // account is AED
            DescriptionRaw = "wrong currency",
            Origin = TxnOrigin.Manual,
            DedupeKey = "test-mixed-currency",
        });
        Db.SaveChanges();

        Assert.Throws<InvalidOperationException>(() => AccountBalanceService.GetBalances(Db, new DateOnly(2026, 7, 31)));
    }
}
