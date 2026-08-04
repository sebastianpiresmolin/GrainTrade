namespace GrainTrade.Abstractions;

// One book per symbol, keyed by the symbol string. Market-order only for now:
// it records executions rather than matching resting bids and asks. Limit
// orders and a real book arrive with Slice 6.
public interface IOrderBookGrain : IGrainWithStringKey
{
    Task Record(Trade trade);

    // Most recent first, capped by the grain.
    Task<IReadOnlyList<Trade>> GetRecentTrades();
}
