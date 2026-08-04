namespace GrainTrade.Abstractions;

// A limit order sitting on the book waiting for a counterparty.
[GenerateSerializer]
public sealed record RestingOrder
{
    [Id(0)]
    public required Guid OrderId { get; init; }

    [Id(1)]
    public required Guid AccountId { get; init; }

    [Id(2)]
    public required string Symbol { get; init; }

    [Id(3)]
    public required OrderSide Side { get; init; }

    [Id(4)]
    public required decimal LimitPrice { get; init; }

    [Id(5)]
    public required int Quantity { get; init; }

    // Falls as the order fills; the order leaves the book at zero.
    [Id(6)]
    public required int Remaining { get; init; }

    // Arrival order breaks price ties.
    [Id(7)]
    public required DateTimeOffset PlacedAt { get; init; }

    [Id(8)]
    public required DateTimeOffset ExpiresAt { get; init; }
}
