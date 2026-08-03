namespace GrainTrade.Abstractions;

// Current price snapshot for one symbol.
[GenerateSerializer]
public sealed record TickerQuote
{
    [Id(0)]
    public required string Symbol { get; init; }

    [Id(1)]
    public required decimal Price { get; init; }

    // Change since the previous tick — lets the UI colour a move without
    // tracking the last value itself.
    [Id(2)]
    public required decimal Change { get; init; }

    [Id(3)]
    public required DateTimeOffset AsOf { get; init; }
}
