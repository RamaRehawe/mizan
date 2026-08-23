namespace Mizan.Models;

public class Account
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public LiquidityClass LiquidityClass { get; set; }
    public required string CurrencyCode { get; set; }
    public string? Institution { get; set; }
    public long OpeningBalanceMinor { get; set; }
    public DateOnly OpeningDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Txn> Txns { get; set; } = [];
}
