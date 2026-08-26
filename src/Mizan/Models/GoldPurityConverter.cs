using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mizan.Models;

/// <summary>GoldPurity can't use SnakeCaseEnumConverter — "24k" isn't a valid C# identifier
/// suffix to derive from a name like K24, so this maps explicitly instead.</summary>
public static class GoldPurityConverter
{
    private static readonly IReadOnlyDictionary<GoldPurity, string> ToDb = new Dictionary<GoldPurity, string>
    {
        [GoldPurity.K24] = "24k",
        [GoldPurity.K22] = "22k",
        [GoldPurity.K21] = "21k",
        [GoldPurity.K18] = "18k",
    };

    private static readonly IReadOnlyDictionary<string, GoldPurity> FromDb =
        ToDb.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static ValueConverter<GoldPurity?, string?> Instance { get; } =
        new(
            v => v.HasValue ? ToDb[v.Value] : null,
            v => v == null ? null : FromDb[v]);
}
