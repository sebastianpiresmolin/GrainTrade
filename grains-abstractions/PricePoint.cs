namespace GrainTrade.Abstractions;

// One observation in a ticker's rolling price history.
[GenerateSerializer]
public sealed record PricePoint
{
    [Id(0)]
    public required decimal Price { get; init; }

    [Id(1)]
    public required DateTimeOffset AsOf { get; init; }
}
