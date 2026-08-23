namespace Mizan.Formatting;

/// <summary>Money is never formatted to a display string in Services/ — that happens here,
/// called only from views. Deliberately not a service: this has no business logic in it.</summary>
public static class DisplayFormat
{
    public static string Aed(long minor) => (minor / 100m).ToString("N2");

    public static string Percent1(decimal rate) => (rate * 100).ToString("N1");

    /// <summary>"ShortTerm" -> "short term" — a human label for an enum, not its stored form.</summary>
    public static string EnumLabel<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var spaced = string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        return spaced.ToLowerInvariant();
    }
}
