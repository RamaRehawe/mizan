using Mizan.Models;

namespace Mizan.Services.Dtos;

public record AccountBalance(int AccountId, string Name, LiquidityClass LiquidityClass, string CurrencyCode, long BalanceMinor);
