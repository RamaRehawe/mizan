using Microsoft.AspNetCore.Mvc;
using Mizan.Models;
using Mizan.Services;
using Mizan.ViewModels;

namespace Mizan.Controllers;

public class CloseController(MizanDbContext db) : Controller
{
    public IActionResult Index()
    {
        var (year, month) = PreviousCompleteMonth(DateOnly.FromDateTime(DateTime.Today));
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        var flows = MonthlyFlowsService.GetFlows(db, year, month);
        var balances = AccountBalanceService.GetBalances(db, monthEnd);

        return View(new CloseViewModel(year, month, flows, balances));
    }

    // The most recently complete calendar month — not a financial calculation, just "which
    // period is this screen showing," so it stays here rather than in a service.
    private static (int Year, int Month) PreviousCompleteMonth(DateOnly today) =>
        today.Month == 1 ? (today.Year - 1, 12) : (today.Year, today.Month - 1);
}
