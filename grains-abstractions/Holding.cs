namespace GrainTrade.Abstractions;

// A position in one symbol. AverageCost is the cost basis, so the UI can show
// unrealised P&L against the live price without replaying the ledger.
[GenerateSerializer]
public sealed record Holding
{
    [Id(0)]
    public required string Symbol { get; init; }

    [Id(1)]
    public required int Quantity { get; init; }

    [Id(2)]
    public required decimal AverageCost { get; init; }

    // Shares committed to resting sell orders — held, but not sellable again.
    [Id(3)]
    public int Reserved { get; init; }

    public int Available => Quantity - Reserved;
}
