namespace Mizan.Models;

/// <summary>The economic fact only — void, correction, and split are operations recorded
/// against a txn in <see cref="TxnVoid"/>, <see cref="TxnSupersession"/>, and
/// <see cref="TxnSplit"/>, never columns here.</summary>
public class Txn : ITimestamped
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

    /// <summary>Free-form provenance set once at creation, never edited — a filename for an
    /// import, a seed run identifier, or null for a plain manual entry. A placeholder ahead of
    /// the real raw_row_id/import_batch audit trail the Import phase will add.</summary>
    public string? SourceDetail { get; set; }

    public required string DedupeKey { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
