namespace GrainTrade.Abstractions;

// One side of a match: what a single account owes or receives. A match between
// two resting orders produces two fills, one per counterparty.
[GenerateSerializer]
public sealed record Fill
{
    [Id(0)]
    public required Guid FillId { get; init; }

    [Id(1)]
    public required Guid OrderId { get; init; }

    [Id(2)]
    public required Guid AccountId { get; init; }

    [Id(3)]
    public required string Symbol { get; init; }

    [Id(4)]
    public required OrderSide Side { get; init; }

    [Id(5)]
    public required int Quantity { get; init; }

    [Id(6)]
    public required decimal Price { get; init; }

    [Id(7)]
    public required DateTimeOffset ExecutedAt { get; init; }

    public decimal Notional => Quantity * Price;
}
