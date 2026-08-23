using System.Security.Cryptography;
using System.Text;

namespace Mizan.Services;

/// <summary>FR-2.8: dedupe_key = hash(account_id, occurred_on, amount_minor,
/// normalized_description, occurrence_index). occurrence_index distinguishes genuinely
/// repeated identical rows on the same day — the seed generator uses this now, and the
/// importer will reuse it unchanged later.</summary>
public static class DedupeKeyGenerator
{
    public static string Compute(int accountId, DateOnly occurredOn, long amountMinor, string normalizedDescription, int occurrenceIndex)
    {
        var canonical = $"{accountId}|{occurredOn:yyyy-MM-dd}|{amountMinor}|{normalizedDescription}|{occurrenceIndex}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    public static string Normalize(string description) =>
        string.Join(' ', description.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
