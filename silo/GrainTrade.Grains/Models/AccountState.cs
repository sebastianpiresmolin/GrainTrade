using GrainTrade.Abstractions;

namespace GrainTrade.Grains.Models;

// Durable state persisted via IPersistentState.
[GenerateSerializer]
public sealed class AccountState
{
    [Id(0)]
    public decimal CashBalance { get; set; }

    // Keyed by symbol. A plain Dictionary, not a concurrent one — the grain is
    // the only writer and Orleans runs one call at a time.
    [Id(1)]
    public Dictionary<string, PositionState> Positions { get; set; } = [];

    // Newest first, capped on write.
    [Id(2)]
    public List<Trade> Trades { get; set; } = [];
}

[GenerateSerializer]
public sealed class PositionState
{
    [Id(0)]
    public int Quantity { get; set; }

    // Total spent on the shares currently held; average cost is derived.
    [Id(1)]
    public decimal CostBasis { get; set; }
}
