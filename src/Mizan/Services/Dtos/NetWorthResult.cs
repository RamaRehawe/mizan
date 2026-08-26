namespace Mizan.Services.Dtos;

public record NetWorthResult(
    DateOnly AsOf, long TotalMinor,
    IReadOnlyList<AccountBalance> ByAccount, IReadOnlyList<HoldingValuation> ByHolding);
