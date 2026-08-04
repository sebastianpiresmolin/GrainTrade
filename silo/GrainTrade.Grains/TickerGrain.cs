using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;
using Orleans.Streams;

namespace GrainTrade.Grains;

public sealed class TickerGrain : Grain, ITickerGrain
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);
    private const int MaxHistory = 120;
    private const double Volatility = 0.01;

    // Ticks between persists. Price is a simulation — losing a few seconds of
    // it on deactivation is acceptable, and writing every 2s per symbol is not.
    private const int TicksPerWrite = 15;

    private readonly IPersistentState<TickerState> _state;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private int _ticksSinceWrite;
    private IAsyncStream<TickerQuote> _quotes = null!;

    public TickerGrain(
        [PersistentState("ticker", "tickers")] IPersistentState<TickerState> state,
        TimeProvider time)
    {
        _state = state;
        _time = time;
        // Seeded per symbol: repeatable prices without a shared static Random.
        _random = new Random(this.GetPrimaryKeyString().GetHashCode());
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var symbol = this.GetPrimaryKeyString();
        _quotes = this.GetStreamProvider(StreamConstants.Provider)
            .GetStream<TickerQuote>(StreamConstants.TickerNamespace, symbol);

        if (!_state.State.Seeded)
        {
            var price = PriceWalk.SeedPrice(this.GetPrimaryKeyString());
            _state.State.Price = price;
            _state.State.PreviousPrice = price;
            _state.State.AsOf = _time.GetUtcNow();
            _state.State.History = [new PricePoint { Price = price, AsOf = _state.State.AsOf }];
            _state.State.Seeded = true;
        }

        // Timer, not reminder: if the grain deactivates, losing the tick is
        // fine — it restarts on next activation and nothing needs waking up.
        this.RegisterGrainTimer(Tick, TickInterval, TickInterval);
        return Task.CompletedTask;
    }

    private async Task Tick(CancellationToken cancellationToken)
    {
        var price = PriceWalk.Next(_state.State.Price, Volatility, _random);
        var now = _time.GetUtcNow();

        _state.State.PreviousPrice = _state.State.Price;
        _state.State.Price = price;
        _state.State.AsOf = now;
        _state.State.History.Add(new PricePoint { Price = price, AsOf = now });

        if (_state.State.History.Count > MaxHistory)
        {
            _state.State.History.RemoveRange(0, _state.State.History.Count - MaxHistory);
        }

        if (++_ticksSinceWrite >= TicksPerWrite)
        {
            _ticksSinceWrite = 0;
            await _state.WriteStateAsync();
        }

        // Side effect of the price change, not the change itself — the state
        // above is already correct whether or not this succeeds.
        await _quotes.OnNextAsync(ToQuote());
    }

    public Task<TickerQuote> GetQuote() => Task.FromResult(ToQuote());

    public Task<IReadOnlyList<PricePoint>> GetHistory() =>
        Task.FromResult<IReadOnlyList<PricePoint>>(_state.State.History.ToArray());

    // Flush the unwritten ticks so a restart resumes near where it left off.
    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_ticksSinceWrite > 0)
        {
            await _state.WriteStateAsync();
        }
    }

    private TickerQuote ToQuote() => new()
    {
        Symbol = this.GetPrimaryKeyString(),
        Price = _state.State.Price,
        Change = _state.State.Price - _state.State.PreviousPrice,
        AsOf = _state.State.AsOf,
    };
}
