namespace Mizan.Models;

/// <summary>Which account holds which asset. One row per account+asset pair — quantity lives
/// entirely in HoldingTxn, never here (INV-6).</summary>
public class Holding : ITimestamped
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public int AssetId { get; set; }
    public Asset? Asset { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
