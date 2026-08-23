namespace Mizan.Models;

public class Txn
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly OccurredOn { get; set; }
    public DateOnly? BookedOn { get; set; }
    public long AmountMinor { get; set; }
    public required string CurrencyCode { get; set; }
    public required string DescriptionRaw { get; set; }
    public string? DescriptionNorm { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public TxnOrigin Origin { get; set; }
    public bool IsVoid { get; set; }
    public string? VoidReason { get; set; }

    // Split parent — unused until Phase 1 of the full requirements doc (post-v0.1).
    public int? ParentTxnId { get; set; }

    // Versioning — corrections insert a new row and set SupersededById on the old one. Never UPDATE amount/date/account.
    public int Version { get; set; } = 1;
    public int? SupersedesId { get; set; }
    public int? SupersededById { get; set; }

    public required string DedupeKey { get; set; }
    public DateTime CreatedAt { get; set; }
}
