using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;
using Orleans.Streams;

namespace GrainTrade.Grains;

// One book per symbol. Owns resting bids/asks and the matching itself: because
// Orleans runs one call at a time per grain, price-time priority holds without
// any locking of the book.
//
// The book never calls an account. A match records fills that accounts claim
// themselves, which keeps the call graph acyclic (Account → Book only).
public sealed class OrderBookGrain : Grain, IOrderBookGrain, IRemindable
{
    private const int MaxTrades = 100;
    private const string ExpiryReminder = "expire-orders";
    private static readonly TimeSpan ExpirySweep = TimeSpan.FromMinutes(1);

    private readonly IPersistentState<OrderBookState> _state;
    private readonly TimeProvider _time;

    // Set on activation. Depth and trades are pushed to subscribers as a side
    // effect of a book change, the same way TickerGrain pushes price ticks.
    private IAsyncStream<BookDepth> _depthStream = null!;
    private IAsyncStream<Trade> _tradeStream = null!;

    public OrderBookGrain(
        [PersistentState("orderbook", "orderbooks")] IPersistentState<OrderBookState> state,
        TimeProvider time)
    {
        _state = state;
        _time = time;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var symbol = this.GetPrimaryKeyString();
        var provider = this.GetStreamProvider(StreamConstants.Provider);
        _depthStream = provider.GetStream<BookDepth>(StreamConstants.DepthNamespace, symbol);
        _tradeStream = provider.GetStream<Trade>(StreamConstants.TradeNamespace, symbol);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Fill>> PlaceLimit(RestingOrder order)
    {
        var fills = new List<Fill>();
        var executed = new List<Trade>();
        var remaining = order.Quantity;
        var now = _time.GetUtcNow();

        foreach (var resting in Matching.Candidates(_state.State.Orders, order.Side, order.LimitPrice))
        {
            if (remaining == 0)
            {
                break;
            }

            var quantity = Math.Min(remaining, resting.Remaining);
            var price = Matching.ExecutionPrice(resting);

            // Two fills per match: one for each side.
            fills.Add(NewFill(order.OrderId, order.AccountId, order.Side, quantity, price, now));
            fills.Add(NewFill(resting.OrderId, resting.AccountId, resting.Side, quantity, price, now));

            Replace(resting with { Remaining = resting.Remaining - quantity });
            remaining -= quantity;

            var trade = new Trade
            {
                TradeId = Guid.NewGuid(),
                AccountId = order.AccountId,
                Symbol = order.Symbol,
                Side = order.Side,
                Quantity = quantity,
                Price = price,
                ExecutedAt = now,
            };
            executed.Add(trade);
            RecordTrade(trade);
        }

        // Whatever didn't match rests.
        if (remaining > 0)
        {
            _state.State.Orders.Add(order with { Remaining = remaining });
        }

        _state.State.Orders.RemoveAll(o => o.Remaining == 0);
        _state.State.UnclaimedFills.AddRange(fills);
        await _state.WriteStateAsync();

        await EnsureExpirySweep();

        // Every placement changes the book; the tape gains a row per match.
        await PublishDepth();
        foreach (var trade in executed)
        {
            await _tradeStream.OnNextAsync(trade);
        }

        return fills;
    }

    public async Task<bool> Cancel(Guid orderId, Guid accountId)
    {
        var index = _state.State.Orders.FindIndex(o => o.OrderId == orderId && o.AccountId == accountId);
        if (index < 0)
        {
            return false;
        }

        _state.State.Orders.RemoveAt(index);
        await _state.WriteStateAsync();

        await PublishDepth();
        return true;
    }

    public async Task<IReadOnlyList<Fill>> ClaimFills(Guid accountId)
    {
        var mine = _state.State.UnclaimedFills.Where(f => f.AccountId == accountId).ToArray();
        if (mine.Length == 0)
        {
            return mine;
        }

        _state.State.UnclaimedFills.RemoveAll(f => f.AccountId == accountId);
        await _state.WriteStateAsync();
        return mine;
    }

    public Task<IReadOnlyList<RestingOrder>> GetOpenOrders(Guid accountId) =>
        Task.FromResult<IReadOnlyList<RestingOrder>>(
            _state.State.Orders.Where(o => o.AccountId == accountId).ToArray());

    public Task<BookDepth> GetDepth() => Task.FromResult(BuildDepth());

    private BookDepth BuildDepth() => new()
    {
        Bids = Matching.Aggregate(_state.State.Orders, OrderSide.Buy),
        Asks = Matching.Aggregate(_state.State.Orders, OrderSide.Sell),
    };

    private Task PublishDepth() => _depthStream.OnNextAsync(BuildDepth());

    public async Task Record(Trade trade)
    {
        RecordTrade(trade);
        await _state.WriteStateAsync();

        // A market order doesn't rest, so depth is unchanged — only the tape.
        await _tradeStream.OnNextAsync(trade);
    }

    public Task<IReadOnlyList<Trade>> GetRecentTrades() =>
        Task.FromResult<IReadOnlyList<Trade>>(_state.State.Trades.ToArray());

    // Reminder, not timer: expiry has to happen even when nothing else wakes
    // this book up, and it must survive a silo restart. A timer would die with
    // the activation, leaving orders resting forever in an untouched book.
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName != ExpiryReminder)
        {
            return;
        }

        var now = _time.GetUtcNow();
        var expired = _state.State.Orders.RemoveAll(o => o.ExpiresAt <= now);
        if (expired > 0)
        {
            await _state.WriteStateAsync();
            await PublishDepth();
        }

        // Nothing left to sweep — stop costing a persisted reminder.
        if (_state.State.Orders.Count == 0)
        {
            var handle = await this.GetReminder(ExpiryReminder);
            if (handle is not null)
            {
                await this.UnregisterReminder(handle);
            }
        }
    }

    private async Task EnsureExpirySweep()
    {
        if (_state.State.Orders.Count == 0)
        {
            return;
        }

        await this.RegisterOrUpdateReminder(ExpiryReminder, ExpirySweep, ExpirySweep);
    }

    private Fill NewFill(
        Guid orderId, Guid accountId, OrderSide side, int quantity, decimal price, DateTimeOffset now) => new()
        {
            FillId = Guid.NewGuid(),
            OrderId = orderId,
            AccountId = accountId,
            Symbol = this.GetPrimaryKeyString(),
            Side = side,
            Quantity = quantity,
            Price = price,
            ExecutedAt = now,
        };

    private void Replace(RestingOrder updated)
    {
        var index = _state.State.Orders.FindIndex(o => o.OrderId == updated.OrderId);
        if (index >= 0)
        {
            _state.State.Orders[index] = updated;
        }
    }

    private void RecordTrade(Trade trade)
    {
        _state.State.Trades.Insert(0, trade);
        if (_state.State.Trades.Count > MaxTrades)
        {
            _state.State.Trades.RemoveRange(MaxTrades, _state.State.Trades.Count - MaxTrades);
        }
    }
}
