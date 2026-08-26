namespace Mizan.Models;

public enum AccountType
{
    Cash,
    Bank,
    Card,
    Broker,
    Loan,
    PhysicalAsset,
    Receivable,
    Other,
}

public enum LiquidityClass
{
    Immediate,
    ShortTerm,
    Illiquid,
    Debt,
}

public enum CategoryKind
{
    Income,
    Expense,
    Transfer,
    Investment,
    Adjustment,
}

public enum TxnOrigin
{
    Import,
    Manual,
    Split,
    Adjustment,
    Seed,
}

public enum AssetClass
{
    Gold,
    Equity,
    Etf,
    Crypto,
    Property,
    Other,
}

public enum AssetUnit
{
    Gram,
    Share,
    Unit,
}

/// <summary>Gold purity. Not a snake-case-of-the-name mapping like the other enums — "24k" isn't
/// a valid C# identifier — so this one uses an explicit dictionary converter instead.</summary>
public enum GoldPurity
{
    K24,
    K22,
    K21,
    K18,
}

public enum HoldingTxnOrigin
{
    Buy,
    Sell,
    Gift,
    Seed,
}

public enum PriceSource
{
    Manual,
    Fetched,
    Estimated,
    Seed,
}

public enum PeriodStatus
{
    Open,
    Closed,
}

public enum SnapshotKind
{
    Close,
    Restatement,
}
