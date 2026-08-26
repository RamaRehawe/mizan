using Mizan.Services.Dtos;

namespace Mizan.ViewModels;

/// <summary>Just a bundle of what two service calls returned — no calculation happens here.
/// That rule belongs to Controllers/ and applies just as much to the model that feeds a view.
/// NetWorth.ByAccount is used for the accounts table too, rather than a separate
/// AccountBalanceService call — for a closed month that keeps every figure on the page reading
/// from the same frozen source, never a mix of live and frozen numbers.</summary>
public record CloseViewModel(int Year, int Month, MonthlyFlows Flows, NetWorthResult NetWorth);
