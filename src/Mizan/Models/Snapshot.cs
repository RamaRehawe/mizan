namespace Mizan.Models;

/// <summary>The frozen net worth total for a closed period (INV-7) — never recomputed with
/// today's prices. PayloadJson is a deliberate escape hatch: a place to freeze something we
/// haven't modeled a column for yet, without a breaking migration later.</summary>
public class Snapshot : ITimestamped
{
    public int Id { get; set; }
    public int PeriodId { get; set; }
    public Period? Period { get; set; }
    public DateTime TakenAt { get; set; }
    public SnapshotKind Kind { get; set; }
    public long TotalNetWorthMinor { get; set; }
    public string? PayloadJson { get; set; }

    // Required for a restatement (FR-9.5) — why this snapshot exists a second time for the
    // same period.
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
