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

    // Cash committed to resting buy orders.
    [Id(3)]
    public decimal ReservedCash { get; set; }

    // Symbols this account has resting orders on — the set of books to poll
    // when settling, so Settle() doesn't fan out to every symbol.
    [Id(4)]
    public HashSet<string> ActiveBooks { get; set; } = [];

    // Fills already applied, so a re-claim can't double-settle.
    [Id(5)]
    public HashSet<Guid> SettledFills { get; set; } = [];
}

[GenerateSerializer]
public sealed class PositionState
{
    [Id(0)]
    public int Quantity { get; set; }

    // Total spent on the shares currently held; average cost is derived.
    [Id(1)]
    public decimal CostBasis { get; set; }

    // Shares committed to resting sell orders.
    [Id(2)]
    public int Reserved { get; set; }
}
