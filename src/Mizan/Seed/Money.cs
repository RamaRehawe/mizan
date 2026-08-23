namespace Mizan.Seed;

internal static class Money
{
    public static long Aed(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
}
