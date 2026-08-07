using GrainTrade.Grains;
using GrainTrade.Grains.Models;
using System.Data;
using System.Globalization;
using GrainTrade.Abstractions;
using Orleans.Runtime;

namespace GrainTrade.Grains;

public sealed class WatchlistGrain : Grain, IWatchlistGrain
{
    public async Task AddSymbol(string symbol)
    {
        _state.State.Symbols.Add(symbol);
        await _state.WriteStateAsync();
    }

    public Task <IReadOnlyList<string>> GetSymbols() =>
        Task.FromResult<IReadOnlyList<string>>(_state.State.Symbols.ToArray());
    public async Task RemoveSymbol(string symbol)
    {
        _state.State.Symbols.Remove(symbol);
        await _state.WriteStateAsync();
    }

    private readonly IPersistentState<WatchlistState> _state;
    private readonly TimeProvider _time;

    public WatchlistGrain(
        [PersistentState("watchlist", "watchlists")] IPersistentState<WatchlistState> state,
        TimeProvider time)
    {
        _state = state;
        _time = time;
    }
}




