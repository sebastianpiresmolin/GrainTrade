namespace GrainTrade.Abstractions;

// A completed execution. Market orders fill immediately at the ticker's
// current price, so an order and its trade are one and the same for now.
[GenerateSerializer]
public sealed record Trade
{
    [Id(0)]
    public required Guid TradeId { get; init; }

    [Id(1)]
    public required Guid AccountId { get; init; }

    [Id(2)]
    public required string Symbol { get; init; }

    [Id(3)]
    public required OrderSide Side { get; init; }

    [Id(4)]
    public required int Quantity { get; init; }

    [Id(5)]
    public required decimal Price { get; init; }

    [Id(6)]
    public required DateTimeOffset ExecutedAt { get; init; }

    public decimal Notional => Quantity * Price;
}
