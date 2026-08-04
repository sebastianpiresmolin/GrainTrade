using GrainTrade.Abstractions;

namespace GrainTrade.Grains.Models;

[GenerateSerializer]
public sealed class OrderBookState
{
    // Resting orders, unsorted — matching sorts by price-time on demand, which
    // keeps insertion cheap and the ordering rule in one place.
    [Id(0)]
    public List<RestingOrder> Orders { get; set; } = [];

    // Fills waiting for their account to claim them. The book can't call an
    // account to settle without risking a cycle, so accounts pull instead.
    [Id(1)]
    public List<Fill> UnclaimedFills { get; set; } = [];

    // Newest first, capped on write.
    [Id(2)]
    public List<Trade> Trades { get; set; } = [];
}
