namespace GrainTrade.Abstractions;

// Resting quantity aggregated per price, for the depth view.
[GenerateSerializer]
public sealed record DepthLevel
{
    [Id(0)]
    public required decimal Price { get; init; }

    [Id(1)]
    public required int Quantity { get; init; }
}

[GenerateSerializer]
public sealed record BookDepth
{
    // Best first: bids descending, asks ascending.
    [Id(0)]
    public IReadOnlyList<DepthLevel> Bids { get; init; } = [];

    [Id(1)]
    public IReadOnlyList<DepthLevel> Asks { get; init; } = [];
}
