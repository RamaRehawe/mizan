using Mizan.Models;
using Mizan.Services.Dtos;

namespace Mizan.Services;

public static class MonthlyFlowsService
{
    /// <summary>Income and expense for one calendar month. Transfers are excluded (INV-5) via
    /// ExcludingTransfers — never re-implement that filter here.</summary>
    public static MonthlyFlows GetFlows(MizanDbContext db, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);

        var rows = db.LiveTxns().ExcludingTransfers()
            .Where(t => t.OccurredOn >= start && t.OccurredOn < end)
            .Select(t => new { t.AmountMinor, t.CurrencyCode, Kind = t.Category!.Kind })
            .ToList();

        // See AccountBalanceService for why this check exists — same INV-8 concern.
        var currencyCodes = rows.Select(r => r.CurrencyCode).Distinct().ToList();
        if (currencyCodes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Mixed currencies in {year:D4}-{month:D2}: {string.Join(", ", currencyCodes)} — not supported yet.");
        }
        var currencyCode = currencyCodes.SingleOrDefault() ?? "AED";

        var incomeMinor = rows.Where(r => r.Kind == CategoryKind.Income).Sum(r => r.AmountMinor);
        var expenseMinor = rows.Where(r => r.Kind == CategoryKind.Expense).Sum(r => r.AmountMinor);
        var netMinor = incomeMinor + expenseMinor;
        var savingsRate = incomeMinor == 0 ? 0m : (decimal)netMinor / incomeMinor;

        return new MonthlyFlows(year, month, currencyCode, incomeMinor, expenseMinor, netMinor, savingsRate);
    }
}
