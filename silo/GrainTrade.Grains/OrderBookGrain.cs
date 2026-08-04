using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;

namespace GrainTrade.Grains;

// One book per symbol. Market orders fill against the ticker price, so this
// records executions rather than matching — resting orders arrive in Slice 6.
public sealed class OrderBookGrain : Grain, IOrderBookGrain
{
    private const int MaxTrades = 100;

    private readonly IPersistentState<OrderBookState> _state;

    public OrderBookGrain(
        [PersistentState("orderbook", "orderbooks")] IPersistentState<OrderBookState> state)
    {
        _state = state;
    }

    public async Task Record(Trade trade)
    {
        _state.State.Trades.Insert(0, trade);

        if (_state.State.Trades.Count > MaxTrades)
        {
            _state.State.Trades.RemoveRange(MaxTrades, _state.State.Trades.Count - MaxTrades);
        }

        // A trade is money that already moved — unlike a simulated price tick,
        // this is written every time.
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<Trade>> GetRecentTrades() =>
        Task.FromResult<IReadOnlyList<Trade>>(_state.State.Trades.ToArray());
}
