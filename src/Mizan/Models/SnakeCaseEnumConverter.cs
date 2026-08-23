using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Mizan.Models;

/// <summary>Persists a C# enum as its snake_case name (e.g. ShortTerm -> "short_term"), never as an int.</summary>
public static class SnakeCaseEnumConverter
{
    public static ValueConverter<TEnum, string> For<TEnum>()
        where TEnum : struct, Enum =>
        new(v => ToSnakeCase(v.ToString()), v => Enum.Parse<TEnum>(ToPascalCase(v)));

    private static string ToSnakeCase(string s) =>
        string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToLowerInvariant();

    private static string ToPascalCase(string s) =>
        string.Concat(s.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
