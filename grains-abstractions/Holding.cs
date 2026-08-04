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
}
