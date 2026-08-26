using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mizan.Models;

/// <summary>SQLite has no real DECIMAL type — a NUMERIC column silently becomes IEEE-754
/// floating point for non-integer values. Quantities and rates are stored as exact decimal
/// strings instead, mapped to C#'s decimal (128-bit, exact) at the boundary. Never a double.</summary>
public static class DecimalTextConverter
{
    public static ValueConverter<decimal, string> Instance { get; } =
        new(v => v.ToString(CultureInfo.InvariantCulture), v => decimal.Parse(v, CultureInfo.InvariantCulture));

    public static ValueConverter<decimal?, string?> NullableInstance { get; } =
        new(
            v => v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : null,
            v => v == null ? null : decimal.Parse(v, CultureInfo.InvariantCulture));
}
