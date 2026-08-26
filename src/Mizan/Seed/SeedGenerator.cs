using Mizan.Models;
using Mizan.Services;

namespace Mizan.Seed;

/// <summary>Deterministic synthetic data — a test fixture, not a demo toy. Same seed value
/// always produces the same accounts, categories, and transactions, so docs/SEED-CHECK.md's
/// expected totals stay valid forever. Idempotent: skips entirely if the database already has
/// accounts, so re-running it by accident never duplicates data.</summary>
public static class SeedGenerator
{
    public const int SeedValue = 424242;

    // The most recently complete month as of when this was written — fixed, not DateTime.Now,
    // so the generated data (and the totals in docs/SEED-CHECK.md) never drift with the clock.
    private static readonly DateOnly AsOf = new(2026, 7, 31);
    private static readonly DateOnly HistoryStart = new(2024, 8, 1);

    public static void Run(MizanDbContext db, string docsPath)
    {
        if (db.Accounts.Any())
        {
            Console.WriteLine("Already seeded — skipping.");
            return;
        }

        var rng = new Random(SeedValue);

        var accounts = CreateAccounts(db);
        var categories = CreateCategories(db);
        var assets = CreateAssets(db);
        db.SaveChanges();

        var holdings = CreateHoldings(db, accounts, assets);
        db.SaveChanges();

        var tracker = new OccurrenceTracker();
        var txns = new List<Txn>();
        var holdingTxns = new List<HoldingTxn>();
        var prices = new List<Price>();
        var periods = new List<Period>();
        var snapshots = new List<Snapshot>();
        var snapshotLines = new List<SnapshotLine>();

        var julyTotals = new MonthTotals();
        var julyNetWorth = new NetWorthMonthResult(0, []);

        // Running tallies, not queries — by construction these equal opening balance (or zero
        // quantity) plus everything added as it's generated, independent of whatever
        // AccountBalanceService/HoldingValuationService/NetWorthService end up computing.
        var balancesAsOfMinor = accounts.Values.ToDictionary(a => a.Name, a => a.OpeningBalanceMinor);
        var quantityByAssetCode = assets.Values.ToDictionary(a => a.Code, _ => 0m);
        var priceByAssetCode = new Dictionary<string, long>
        {
            ["XAU22K"] = Money.Aed(250.00m),
            ["GEF"] = Money.Aed(100.00m),
        };

        for (var month = new DateOnly(HistoryStart.Year, HistoryStart.Month, 1); month <= AsOf; month = month.AddMonths(1))
        {
            var isJuly = month.Year == AsOf.Year && month.Month == AsOf.Month;
            var monthEnd = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

            var monthTxns = GenerateMonth(month, accounts, categories, rng, tracker);
            txns.AddRange(monthTxns);
            foreach (var t in monthTxns)
            {
                balancesAsOfMinor[t.Account!.Name] += t.AmountMinor;
            }
            if (isJuly)
            {
                julyTotals = Tally(monthTxns);
            }

            // Prices drift a little every month — deterministic, not a flat demo line.
            foreach (var asset in assets.Values)
            {
                priceByAssetCode[asset.Code] = NextPrice(rng, priceByAssetCode[asset.Code]);
                prices.Add(new Price
                {
                    Asset = asset,
                    AsOfDate = monthEnd,
                    Source = PriceSource.Seed,
                    PriceMinor = priceByAssetCode[asset.Code],
                    CurrencyCode = "AED",
                });
            }

            // A small recurring purchase into each holding every month, paid for from
            // Bank - Current — kind=investment, so ExcludingTransfers() already keeps this out
            // of income/expense without any change to that filter.
            foreach (var holding in holdings.Values)
            {
                var asset = holding.Asset!;
                var qty = NextPurchaseQty(rng, asset.Code);
                var priceMinor = priceByAssetCode[asset.Code];
                var costMinor = (long)Math.Round(qty * priceMinor);
                var day = new DateOnly(month.Year, month.Month, 20);
                var purchaseCategory = categories[asset.AssetClass == AssetClass.Gold ? "Gold Purchase" : "Fund Purchase"];

                var cashTxn = MakeTxn(tracker, accounts["Bank - Current"], day, -(costMinor / 100m), $"{asset.Name} purchase", purchaseCategory);
                txns.Add(cashTxn);
                balancesAsOfMinor[cashTxn.Account!.Name] += cashTxn.AmountMinor;

                holdingTxns.Add(new HoldingTxn
                {
                    Holding = holding,
                    OccurredOn = day,
                    QtyDelta = qty,
                    UnitCostMinor = priceMinor,
                    FeeMinor = 0,
                    CurrencyCode = "AED",
                    LinkedTxn = cashTxn,
                    Origin = HoldingTxnOrigin.Seed,
                });
                quantityByAssetCode[asset.Code] += qty;
            }

            // Close the period. Every month generated here is already in the past relative to
            // AsOf, so every one of them gets closed — this is what freezes a snapshot (INV-7).
            var period = new Period { Year = month.Year, Month = month.Month, Status = PeriodStatus.Closed, ClosedAt = DateTime.UtcNow, IsStale = false };
            periods.Add(period);

            var holdingValueByCode = holdings.Values.ToDictionary(
                h => h.Asset!.Code,
                h => (long)Math.Round(quantityByAssetCode[h.Asset!.Code] * priceByAssetCode[h.Asset!.Code]));

            var totalNetWorthMinor = balancesAsOfMinor.Values.Sum() + holdingValueByCode.Values.Sum();

            var snapshot = new Snapshot { Period = period, TakenAt = DateTime.UtcNow, Kind = SnapshotKind.Close, TotalNetWorthMinor = totalNetWorthMinor };
            snapshots.Add(snapshot);

            foreach (var account in accounts.Values)
            {
                snapshotLines.Add(new SnapshotLine
                {
                    Snapshot = snapshot,
                    Account = account,
                    BalanceMinor = balancesAsOfMinor[account.Name],
                    BalanceBaseMinor = balancesAsOfMinor[account.Name],
                });
            }
            foreach (var holding in holdings.Values)
            {
                var code = holding.Asset!.Code;
                snapshotLines.Add(new SnapshotLine
                {
                    Snapshot = snapshot,
                    Account = holding.Account,
                    Asset = holding.Asset,
                    Quantity = quantityByAssetCode[code],
                    PriceMinor = priceByAssetCode[code],
                    PriceAsOf = monthEnd,
                    BalanceMinor = holdingValueByCode[code],
                    BalanceBaseMinor = holdingValueByCode[code],
                });
            }

            if (isJuly)
            {
                julyNetWorth = new NetWorthMonthResult(
                    totalNetWorthMinor,
                    [.. holdings.Values.Select(h => new HoldingSnapshotForCheck(
                        h.Asset!.Name, quantityByAssetCode[h.Asset!.Code], priceByAssetCode[h.Asset!.Code], holdingValueByCode[h.Asset!.Code]))]);
            }
        }

        db.Txns.AddRange(txns);
        db.HoldingTxns.AddRange(holdingTxns);
        db.Prices.AddRange(prices);
        db.Periods.AddRange(periods);
        db.Snapshots.AddRange(snapshots);
        db.SnapshotLines.AddRange(snapshotLines);
        db.SaveChanges();

        Console.WriteLine($"Seeded {accounts.Count} accounts, {categories.Count} categories, {assets.Count} assets, " +
            $"{holdings.Count} holdings, {txns.Count} transactions, {periods.Count} closed periods " +
            $"({HistoryStart:yyyy-MM} through {AsOf:yyyy-MM}).");

        WriteSeedCheck(docsPath, accounts, julyTotals, balancesAsOfMinor, julyNetWorth);
    }

    private static Dictionary<string, Account> CreateAccounts(MizanDbContext db)
    {
        Account[] accounts =
        [
            new()
            {
                Name = "Bank - Current", Type = AccountType.Bank, LiquidityClass = LiquidityClass.Immediate,
                CurrencyCode = "AED", OpeningBalanceMinor = Money.Aed(5_000.00m), OpeningDate = HistoryStart,
            },
            new()
            {
                Name = "Bank - Savings", Type = AccountType.Bank, LiquidityClass = LiquidityClass.ShortTerm,
                CurrencyCode = "AED", OpeningBalanceMinor = Money.Aed(20_000.00m), OpeningDate = HistoryStart,
            },
            new()
            {
                Name = "Cash on hand", Type = AccountType.Cash, LiquidityClass = LiquidityClass.Immediate,
                CurrencyCode = "AED", OpeningBalanceMinor = Money.Aed(500.00m), OpeningDate = HistoryStart,
            },
            new()
            {
                Name = "Card - Visa", Type = AccountType.Card, LiquidityClass = LiquidityClass.Debt,
                CurrencyCode = "AED", OpeningBalanceMinor = 0, OpeningDate = HistoryStart,
            },
            new()
            {
                // Holds the gold holding. No currency of its own beyond AED — cash never
                // touches this account directly, only via the linked txn on Bank - Current.
                Name = "Locker", Type = AccountType.PhysicalAsset, LiquidityClass = LiquidityClass.Illiquid,
                CurrencyCode = "AED", OpeningBalanceMinor = 0, OpeningDate = HistoryStart,
            },
            new()
            {
                Name = "Broker", Type = AccountType.Broker, LiquidityClass = LiquidityClass.Illiquid,
                CurrencyCode = "AED", OpeningBalanceMinor = 0, OpeningDate = HistoryStart,
            },
        ];
        db.Accounts.AddRange(accounts);
        return accounts.ToDictionary(a => a.Name);
    }

    private static Dictionary<string, Category> CreateCategories(MizanDbContext db)
    {
        var income = new Category { Name = "Income", Kind = CategoryKind.Income };
        var living = new Category { Name = "Living", Kind = CategoryKind.Expense };
        var transfers = new Category { Name = "Transfers", Kind = CategoryKind.Transfer };
        var investments = new Category { Name = "Investments", Kind = CategoryKind.Investment };
        db.Categories.AddRange(income, living, transfers, investments);

        Category[] leaves =
        [
            new() { Name = "Salary", Kind = CategoryKind.Income, Parent = income },
            new() { Name = "Rent", Kind = CategoryKind.Expense, Parent = living },
            new() { Name = "Utilities", Kind = CategoryKind.Expense, Parent = living },
            new() { Name = "Groceries", Kind = CategoryKind.Expense, Parent = living },
            new() { Name = "Dining", Kind = CategoryKind.Expense, Parent = living },
            new() { Name = "Savings Transfer", Kind = CategoryKind.Transfer, Parent = transfers },
            new() { Name = "Card Payment", Kind = CategoryKind.Transfer, Parent = transfers },
            new() { Name = "Cash Withdrawal", Kind = CategoryKind.Transfer, Parent = transfers },
            // Investment purchases are their own kind (D5) — not spending, not a pure transfer.
            // ExcludingTransfers() already keeps kind=investment out of income/expense, same as
            // kind=transfer, with no change needed to that filter.
            new() { Name = "Gold Purchase", Kind = CategoryKind.Investment, Parent = investments },
            new() { Name = "Fund Purchase", Kind = CategoryKind.Investment, Parent = investments },
        ];
        db.Categories.AddRange(leaves);

        var all = new[] { income, living, transfers, investments }.Concat(leaves);
        return all.ToDictionary(c => c.Name);
    }

    private static Dictionary<string, Asset> CreateAssets(MizanDbContext db)
    {
        Asset[] assets =
        [
            new() { Code = "XAU22K", Name = "Gold 22k", AssetClass = AssetClass.Gold, Unit = AssetUnit.Gram, Purity = GoldPurity.K22, QuoteCurrencyCode = "AED" },
            new() { Code = "GEF", Name = "Global Equity Fund", AssetClass = AssetClass.Etf, Unit = AssetUnit.Share, QuoteCurrencyCode = "AED" },
        ];
        db.Assets.AddRange(assets);
        return assets.ToDictionary(a => a.Code);
    }

    private static Dictionary<string, Holding> CreateHoldings(MizanDbContext db, Dictionary<string, Account> accounts, Dictionary<string, Asset> assets)
    {
        Holding[] holdings =
        [
            new() { Account = accounts["Locker"], Asset = assets["XAU22K"] },
            new() { Account = accounts["Broker"], Asset = assets["GEF"] },
        ];
        db.Holdings.AddRange(holdings);
        return holdings.ToDictionary(h => h.Asset!.Code);
    }

    private static List<Txn> GenerateMonth(
        DateOnly month, Dictionary<string, Account> accounts, Dictionary<string, Category> categories,
        Random rng, OccurrenceTracker tracker)
    {
        var current = accounts["Bank - Current"];
        var savings = accounts["Bank - Savings"];
        var cash = accounts["Cash on hand"];
        var card = accounts["Card - Visa"];
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

        var txns = new List<Txn>
        {
            MakeTxn(tracker, current, new DateOnly(month.Year, month.Month, 1), 27_500.00m, "Salary transfer", categories["Salary"]),
            MakeTxn(tracker, current, new DateOnly(month.Year, month.Month, 1), -6_000.00m, "Rent", categories["Rent"]),
            MakeTxn(tracker, current, new DateOnly(month.Year, month.Month, 5), -450.00m, "DEWA - utilities", categories["Utilities"]),
        };

        string[] groceryMerchants = ["Carrefour", "Spinneys", "Lulu Hypermarket", "Waitrose"];
        var groceryCount = rng.Next(3, 6);
        for (var i = 0; i < groceryCount; i++)
        {
            var amount = rng.Next(15_000, 40_001) / 100m;
            var day = new DateOnly(month.Year, month.Month, rng.Next(1, daysInMonth + 1));
            var merchant = groceryMerchants[rng.Next(groceryMerchants.Length)];
            txns.Add(MakeTxn(tracker, current, day, -amount, merchant, categories["Groceries"]));
        }

        string[] diningMerchants = ["Costa Coffee", "Wagamama", "Local Bites", "Zaatar w Zeit"];
        var diningCount = rng.Next(2, 5);
        for (var i = 0; i < diningCount; i++)
        {
            var amount = rng.Next(6_000, 22_001) / 100m;
            var day = new DateOnly(month.Year, month.Month, rng.Next(1, daysInMonth + 1));
            var merchant = diningMerchants[rng.Next(diningMerchants.Length)];
            txns.Add(MakeTxn(tracker, cash, day, -amount, merchant, categories["Dining"]));
        }

        // Card purchases accrue debt through the month; the payment below pays the card off in
        // full, so the card balance returns to zero every month end.
        string[] cardMerchants = ["Amazon.ae", "Noon", "Talabat", "Careem"];
        var cardPurchaseTotal = 0m;
        var cardPurchaseCount = rng.Next(2, 5);
        for (var i = 0; i < cardPurchaseCount; i++)
        {
            var amount = rng.Next(10_000, 45_001) / 100m;
            cardPurchaseTotal += amount;
            var day = new DateOnly(month.Year, month.Month, rng.Next(1, daysInMonth + 1));
            var merchant = cardMerchants[rng.Next(cardMerchants.Length)];
            var category = i % 2 == 0 ? categories["Groceries"] : categories["Dining"];
            txns.Add(MakeTxn(tracker, card, day, -amount, merchant, category));
        }

        var transferDay = new DateOnly(month.Year, month.Month, 5);
        txns.Add(MakeTxn(tracker, current, transferDay, -8_000.00m, "Transfer to savings", categories["Savings Transfer"]));
        txns.Add(MakeTxn(tracker, savings, transferDay, 8_000.00m, "Transfer from current", categories["Savings Transfer"]));

        var paymentDay = new DateOnly(month.Year, month.Month, Math.Min(28, daysInMonth));
        txns.Add(MakeTxn(tracker, current, paymentDay, -cardPurchaseTotal, "Card payment", categories["Card Payment"]));
        txns.Add(MakeTxn(tracker, card, paymentDay, cardPurchaseTotal, "Card payment received", categories["Card Payment"]));

        // Without this, cash on hand only ever gets spent from (dining) and never topped up —
        // it drains to a nonsensical negative balance over 24 months. A monthly ATM withdrawal
        // keeps it funded, the same way a real cash account works.
        var atmDay = new DateOnly(month.Year, month.Month, 10);
        txns.Add(MakeTxn(tracker, current, atmDay, -500.00m, "ATM withdrawal", categories["Cash Withdrawal"]));
        txns.Add(MakeTxn(tracker, cash, atmDay, 500.00m, "ATM withdrawal", categories["Cash Withdrawal"]));

        return txns;
    }

    private static Txn MakeTxn(OccurrenceTracker tracker, Account account, DateOnly date, decimal amountAed, string description, Category category)
    {
        var amountMinor = Money.Aed(amountAed);
        var normalized = DedupeKeyGenerator.Normalize(description);
        var occurrence = tracker.Next(account.Id, date, amountMinor, normalized);
        return new Txn
        {
            Account = account,
            OccurredOn = date,
            AmountMinor = amountMinor,
            CurrencyCode = "AED",
            DescriptionRaw = description,
            DescriptionNorm = normalized,
            Category = category,
            Origin = TxnOrigin.Seed,
            SourceDetail = $"seed run {SeedValue}",
            DedupeKey = DedupeKeyGenerator.Compute(account.Id, date, amountMinor, normalized, occurrence),
        };
    }

    // -1% to +3% per month — deterministic drift (given the seeded rng), not a flat line and
    // not wild noise.
    private static long NextPrice(Random rng, long currentPriceMinor)
    {
        var changePct = (rng.NextDouble() * 0.04) - 0.01;
        var next = (long)Math.Round(currentPriceMinor * (1 + changePct));
        return Math.Max(next, 1);
    }

    private static decimal NextPurchaseQty(Random rng, string assetCode) => assetCode switch
    {
        "XAU22K" => rng.Next(30, 81) / 100m, // 0.30 - 0.80 g
        "GEF" => rng.Next(300, 801) / 100m, // 3.00 - 8.00 units
        _ => throw new ArgumentOutOfRangeException(nameof(assetCode)),
    };

    private static MonthTotals Tally(List<Txn> monthTxns)
    {
        var totals = new MonthTotals();
        foreach (var t in monthTxns)
        {
            var kind = t.Category!.Kind;
            if (kind == CategoryKind.Income)
            {
                totals.IncomeMinor += t.AmountMinor;
            }
            else if (kind == CategoryKind.Expense)
            {
                totals.ExpenseMinor += t.AmountMinor;
            }
        }
        return totals;
    }

    private static void WriteSeedCheck(
        string docsPath, Dictionary<string, Account> accounts, MonthTotals july,
        Dictionary<string, long> balancesAsOfMinor, NetWorthMonthResult julyNetWorth)
    {
        var balances = accounts.Values
            .OrderBy(a => a.Name)
            .Select(a => (a.Name, OpeningMinor: a.OpeningBalanceMinor, AsOfMinor: balancesAsOfMinor[a.Name]))
            .ToList();

        var netIncomeMinor = july.IncomeMinor + july.ExpenseMinor;
        var savingsRate = july.IncomeMinor == 0 ? 0m : (decimal)netIncomeMinor / july.IncomeMinor;

        var content = $"""
            # Seed check — expected totals for {AsOf:yyyy-MM}

            Generated by `SeedGenerator` (seed value `{SeedValue}`) — do not hand-edit. If the
            seed generator changes, regenerate this file (`dotnet run -- seed`) rather than
            editing the numbers here; that's the whole point of it being a ground truth
            independent of whatever the service layer computes.

            ## Monthly flows, {AsOf:yyyy-MM} (MonthlyFlowsService must match exactly)

            | | AED |
            |---|---:|
            | Income | {july.IncomeMinor / 100m:N2} |
            | Expense | {-july.ExpenseMinor / 100m:N2} |
            | Net | {netIncomeMinor / 100m:N2} |
            | Savings rate | {savingsRate:P1} |

            Transfers (the monthly savings transfer and card payment) and investment purchases
            (gold, fund) are excluded from both income and expense above, by construction —
            neither `transfer` nor `investment` kind categories are ever summed into
            `julyTotals`.

            ## Account balances as of {AsOf:yyyy-MM-dd} (AccountBalanceService must match exactly)

            | Account | Opening (AED) | As of {AsOf:yyyy-MM-dd} (AED) |
            |---|---:|---:|
            {string.Join("\n", balances.Select(b => $"| {b.Name} | {b.OpeningMinor / 100m:N2} | {b.AsOfMinor / 100m:N2} |"))}

            The "as of" column is the seed generator's own running tally — opening balance plus
            every transaction added as it was generated, month by month — not a query against
            what got inserted. Locker and Broker sit at zero here on purpose: their value is
            entirely in the holdings below, not in a cash balance.

            ## Holdings as of {AsOf:yyyy-MM-dd} (HoldingValuationService must match exactly)

            | Asset | Quantity | Price used | Value (AED) |
            |---|---:|---:|---:|
            {string.Join("\n", julyNetWorth.Holdings.Select(h => $"| {h.AssetName} | {h.Quantity:0.####} | {h.PriceMinor / 100m:N2} | {h.ValueMinor / 100m:N2} |"))}

            Quantity is the generator's own running sum of every purchase it made, month by
            month — never a stored total (INV-6). Price is that asset's price as of
            {AsOf:yyyy-MM-dd}, from the `price` table's `seed` rows.

            ## Net worth as of {AsOf:yyyy-MM-dd} (NetWorthService must match exactly)

            **{julyNetWorth.TotalMinor / 100m:N2} AED**

            Every account balance above, plus every holding value above, added together —
            computed the same running-tally way, never a query. This is also exactly what got
            frozen into July's `snapshot` row when the generator closed that period; reading it
            back after the fact (even after later months' prices moved) must still give this
            number.
            """;

        Directory.CreateDirectory(docsPath);
        File.WriteAllText(Path.Combine(docsPath, "SEED-CHECK.md"), content);
        Console.WriteLine($"Wrote {Path.Combine(docsPath, "SEED-CHECK.md")}");
    }

    private sealed class MonthTotals
    {
        public long IncomeMinor;
        public long ExpenseMinor;
    }

    private sealed record HoldingSnapshotForCheck(string AssetName, decimal Quantity, long PriceMinor, long ValueMinor);

    private sealed record NetWorthMonthResult(long TotalMinor, IReadOnlyList<HoldingSnapshotForCheck> Holdings);

    private sealed class OccurrenceTracker
    {
        private readonly Dictionary<(int, DateOnly, long, string), int> _seen = [];

        public int Next(int accountId, DateOnly occurredOn, long amountMinor, string normalizedDescription)
        {
            var key = (accountId, occurredOn, amountMinor, normalizedDescription);
            var index = _seen.GetValueOrDefault(key, 0);
            _seen[key] = index + 1;
            return index;
        }
    }
}
