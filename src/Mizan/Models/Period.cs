namespace Mizan.Models;

/// <summary>One calendar month. Closing a period is the action that freezes a Snapshot — see
/// PeriodService.</summary>
public class Period : ITimestamped
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public PeriodStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsStale { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
