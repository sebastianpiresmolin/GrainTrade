namespace GrainTrade.Grains.Models;

// Durable state persisted via IPersistentState.
[GenerateSerializer]
public sealed class AccountState
{
    [Id(0)]
    public decimal CashBalance { get; set; }
}
