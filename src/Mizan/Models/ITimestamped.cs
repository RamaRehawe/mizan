namespace Mizan.Models;

/// <summary>Both columns are database-managed — never set from application code. See
/// MizanDbContext.ConfigureTimestamps for how.</summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
