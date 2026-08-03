using GrainTrade.Abstractions;

namespace GrainTrade.Grains.Models;

// Durable state persisted via IPersistentState.
[GenerateSerializer]
public sealed class TickerState
{
    [Id(0)]
    public decimal Price { get; set; }

    [Id(1)]
    public decimal PreviousPrice { get; set; }

    [Id(2)]
    public DateTimeOffset AsOf { get; set; }

    // Rolling window, oldest first. Trimmed on write.
    [Id(3)]
    public List<PricePoint> History { get; set; } = [];

    [Id(4)]
    public bool Seeded { get; set; }
}
