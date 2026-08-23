using Mizan.Models;
using Mizan.Services.Dtos;

namespace Mizan.Services;

public static class AccountBalanceService
{
    /// <summary>Balance per active account as of the given date: opening balance plus every
    /// live txn on or before that date. Always returns every active account, even ones with
    /// no transactions at all.</summary>
    public static IReadOnlyList<AccountBalance> GetBalances(MizanDbContext db, DateOnly asOf)
    {
        var accounts = db.Accounts.Where(a => a.IsActive).OrderBy(a => a.SortOrder).ThenBy(a => a.Name).ToList();
        var result = new List<AccountBalance>(accounts.Count);

        foreach (var account in accounts)
        {
            var txns = db.LiveTxns()
                .Where(t => t.AccountId == account.Id && t.OccurredOn <= asOf)
                .Select(t => new { t.AmountMinor, t.CurrencyCode })
                .ToList();

            // INV-8: no reported figure sums across currencies without a stated rate. v0.1 is
            // single-currency by construction (nothing but the seed generator writes txns, and
            // it always matches the account's own currency), but this guards the day something
            // else starts writing txns and gets that wrong.
            var mismatched = txns.Select(t => t.CurrencyCode).Where(c => c != account.CurrencyCode).Distinct().ToList();
            if (mismatched.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Account {account.Id} ({account.CurrencyCode}) has txns in {string.Join(", ", mismatched)} — mixed-currency balances aren't supported yet.");
            }

            var balanceMinor = account.OpeningBalanceMinor + txns.Sum(t => t.AmountMinor);
            result.Add(new AccountBalance(account.Id, account.Name, account.LiquidityClass, account.CurrencyCode, balanceMinor));
        }

        return result;
    }
}
