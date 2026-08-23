using Mizan.Services.Dtos;

namespace Mizan.ViewModels;

/// <summary>Just a bundle of what two service calls returned — no calculation happens here.
/// That rule belongs to Controllers/ and applies just as much to the model that feeds a view.</summary>
public record CloseViewModel(int Year, int Month, MonthlyFlows Flows, IReadOnlyList<AccountBalance> Balances);
