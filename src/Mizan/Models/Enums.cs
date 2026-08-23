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
