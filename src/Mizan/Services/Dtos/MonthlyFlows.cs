namespace Mizan.Services.Dtos;

public record MonthlyFlows(int Year, int Month, string CurrencyCode, long IncomeMinor, long ExpenseMinor, long NetMinor, decimal SavingsRate);
