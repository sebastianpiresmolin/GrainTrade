using GrainTrade.Abstractions;

namespace GrainTrade.Grains.Models;

[GenerateSerializer]
public sealed class OrderBookState
{
    // Newest first, capped on write.
    [Id(0)]
    public List<Trade> Trades { get; set; } = [];
}
