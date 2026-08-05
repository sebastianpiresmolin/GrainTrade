using System.Globalization;
using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;

namespace GrainTrade.Grains;

public sealed class AccountGrain : Grain, IAccountGrain
{
    private const int MaxTrades = 100;
    private static readonly TimeSpan OrderLifetime = TimeSpan.FromMinutes(10);

    private readonly IPersistentState<AccountState> _state;
    private readonly TimeProvider _time;

    // "accounts" is the storage provider the silo host registers.
    public AccountGrain(
        [PersistentState("account", "accounts")] IPersistentState<AccountState> state,
        TimeProvider time)
    {
        _state = state;
        _time = time;
    }

    public async Task<AccountSummary> Deposit(decimal amount)
    {
        RequirePositive(amount);
        _state.State.CashBalance += amount;
        await _state.WriteStateAsync();
        return ToSummary();
    }

    public async Task<AccountSummary> Withdraw(decimal amount)
    {
        RequirePositive(amount);
        if (amount > _state.State.CashBalance)
        {
            throw new InvalidOperationException(
                $"Insufficient funds: cannot withdraw {Money(amount)} from a balance of {Money(_state.State.CashBalance)}.");
        }
        _state.State.CashBalance -= amount;
        await _state.WriteStateAsync();
        return ToSummary();
    }

    public Task<AccountSummary> GetSummary() => Task.FromResult(ToSummary());

    public async Task<OrderResult> PlaceOrder(string symbol, OrderSide side, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }

        // Price first: this is the only call that leaves the grain, and doing
        // it before any mutation means a slow ticker can't leave half-applied
        // state behind. Calls fan out to other grains and never back here.
        var quote = await GrainFactory.GetGrain<ITickerGrain>(symbol).GetQuote();
        var price = quote.Price;
        var notional = quantity * price;

        _state.State.Positions.TryGetValue(symbol, out var position);

        // Reserved cash and shares back resting limit orders, so they aren't
        // available to spend twice.
        if (side == OrderSide.Buy)
        {
            var available = _state.State.CashBalance - _state.State.ReservedCash;
            if (notional > available)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds: {quantity} {symbol} at {Money(price)} costs {Money(notional)}, available is {Money(available)}.");
            }
        }
        else
        {
            var available = (position?.Quantity ?? 0) - (position?.Reserved ?? 0);
            if (available < quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient shares: cannot sell {quantity} {symbol}, {available} available.");
            }
        }

        var trade = new Trade
        {
            TradeId = Guid.NewGuid(),
            AccountId = this.GetPrimaryKey(),
            Symbol = symbol,
            Side = side,
            Quantity = quantity,
            Price = price,
            ExecutedAt = _time.GetUtcNow(),
        };

        Apply(trade, position);

        _state.State.Trades.Insert(0, trade);
        if (_state.State.Trades.Count > MaxTrades)
        {
            _state.State.Trades.RemoveRange(MaxTrades, _state.State.Trades.Count - MaxTrades);
        }

        // Cash and holdings move together in one write.
        await _state.WriteStateAsync();

        // The account is already settled; the book is a record of it.
        await GrainFactory.GetGrain<IOrderBookGrain>(symbol).Record(trade);

        return new OrderResult { Trade = trade, Account = ToSummary() };
    }

    private void Apply(Trade trade, PositionState? position)
    {
        if (trade.Side == OrderSide.Buy)
        {
            _state.State.CashBalance -= trade.Notional;

            position ??= _state.State.Positions[trade.Symbol] = new PositionState();
            position.Quantity += trade.Quantity;
            position.CostBasis += trade.Notional;
            return;
        }

        _state.State.CashBalance += trade.Notional;

        // Sells reduce the basis proportionally, so average cost is unchanged
        // by selling — only buys move it.
        position!.CostBasis -= position.CostBasis * trade.Quantity / position.Quantity;
        position.Quantity -= trade.Quantity;

        if (position.Quantity == 0)
        {
            _state.State.Positions.Remove(trade.Symbol);
        }
    }

    public async Task<OrderResult> PlaceLimitOrder(
        string symbol, OrderSide side, int quantity, decimal limitPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }
        if (limitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitPrice), limitPrice, "Limit price must be greater than zero.");
        }

        // Settle first so reservations are checked against current reality.
        await SettleInternal();

        var now = _time.GetUtcNow();
        var notional = quantity * limitPrice;
        _state.State.Positions.TryGetValue(symbol, out var position);

        if (side == OrderSide.Buy)
        {
            var available = _state.State.CashBalance - _state.State.ReservedCash;
            if (notional > available)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds: {quantity} {symbol} at {Money(limitPrice)} needs {Money(notional)}, available is {Money(available)}.");
            }
            _state.State.ReservedCash += notional;
        }
        else
        {
            var available = (position?.Quantity ?? 0) - (position?.Reserved ?? 0);
            if (available < quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient shares: cannot sell {quantity} {symbol}, {available} available.");
            }
            position!.Reserved += quantity;
        }

        var order = new RestingOrder
        {
            OrderId = Guid.NewGuid(),
            AccountId = this.GetPrimaryKey(),
            Symbol = symbol,
            Side = side,
            LimitPrice = limitPrice,
            Quantity = quantity,
            Remaining = quantity,
            PlacedAt = now,
            ExpiresAt = now.Add(OrderLifetime),
        };

        _state.State.ActiveBooks.Add(symbol);
        await _state.WriteStateAsync();

        // Fan out: the book matches and records fills. It never calls back.
        var fills = await GrainFactory.GetGrain<IOrderBookGrain>(symbol).PlaceLimit(order);

        // Anything that matched immediately settles now rather than waiting.
        if (fills.Count > 0)
        {
            await SettleInternal();
        }

        return new OrderResult
        {
            Trade = ToTrade(order, fills, now),
            Account = ToSummary(),
        };
    }

    public async Task<bool> CancelLimitOrder(string symbol, Guid orderId)
    {
        var open = await GrainFactory.GetGrain<IOrderBookGrain>(symbol).GetOpenOrders(this.GetPrimaryKey());
        var order = open.FirstOrDefault(o => o.OrderId == orderId);
        if (order is null)
        {
            return false;
        }

        if (!await GrainFactory.GetGrain<IOrderBookGrain>(symbol).Cancel(orderId, this.GetPrimaryKey()))
        {
            return false;
        }

        ReleaseReservation(order, order.Remaining);
        await _state.WriteStateAsync();
        return true;
    }

    public async Task<AccountSummary> Settle()
    {
        await SettleInternal();
        return ToSummary();
    }

    public async Task<IReadOnlyList<RestingOrder>> GetOpenOrders()
    {
        var books = _state.State.ActiveBooks.ToArray();
        var orders = await Task.WhenAll(books.Select(s =>
            GrainFactory.GetGrain<IOrderBookGrain>(s).GetOpenOrders(this.GetPrimaryKey())));

        return orders.SelectMany(o => o).OrderBy(o => o.PlacedAt).ToArray();
    }

    // Pulls fills from every book this account rests orders on, applies them,
    // then reconciles reservations to whatever is actually still resting. Fills
    // are handed over once (SettledFills also guards a re-claim), so nothing
    // double-applies.
    private async Task SettleInternal()
    {
        var books = _state.State.ActiveBooks.ToArray();

        // No resting orders anywhere ⇒ nothing can be reserved. Handles an
        // account whose orders all expired on the book while it sat idle.
        if (books.Length == 0)
        {
            if (ReconcileReservations([]))
            {
                await _state.WriteStateAsync();
            }
            return;
        }

        var claimed = await Task.WhenAll(books.Select(s =>
            GrainFactory.GetGrain<IOrderBookGrain>(s).ClaimFills(this.GetPrimaryKey())));
        var openPerBook = await Task.WhenAll(books.Select(s =>
            GrainFactory.GetGrain<IOrderBookGrain>(s).GetOpenOrders(this.GetPrimaryKey())));

        var fills = claimed.SelectMany(f => f)
            .Where(f => _state.State.SettledFills.Add(f.FillId))
            .OrderBy(f => f.ExecutedAt)
            .ToArray();

        foreach (var fill in fills)
        {
            ApplyFill(fill);
        }

        var changed = fills.Length > 0;
        changed |= ReconcileReservations(openPerBook.SelectMany(o => o));

        // Drop books nothing rests on any more, so Settle() stops polling them.
        for (var i = 0; i < books.Length; i++)
        {
            if (openPerBook[i].Count == 0)
            {
                changed |= _state.State.ActiveBooks.Remove(books[i]);
            }
        }

        if (changed)
        {
            await _state.WriteStateAsync();
        }
    }

    // Reservations always equal what's committed to still-open orders. Recomputing
    // them (rather than decrementing per fill) is what releases cash left over when
    // a buy fills below its limit, and cash/shares tied to an order that expired on
    // the book — neither of which the account is told about directly. Returns
    // whether anything changed.
    private bool ReconcileReservations(IEnumerable<RestingOrder> open)
    {
        var openList = open.ToList();

        var reservedCash = openList
            .Where(o => o.Side == OrderSide.Buy)
            .Sum(o => o.Remaining * o.LimitPrice);

        var reservedShares = openList
            .Where(o => o.Side == OrderSide.Sell)
            .GroupBy(o => o.Symbol)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Remaining));

        var changed = _state.State.ReservedCash != reservedCash;
        _state.State.ReservedCash = reservedCash;

        foreach (var (symbol, position) in _state.State.Positions)
        {
            var shares = reservedShares.GetValueOrDefault(symbol);
            if (position.Reserved != shares)
            {
                position.Reserved = shares;
                changed = true;
            }
        }

        return changed;
    }

    private void ApplyFill(Fill fill)
    {
        _state.State.Positions.TryGetValue(fill.Symbol, out var position);

        if (fill.Side == OrderSide.Buy)
        {
            // Charge what it actually cost; the reservation is squared up
            // afterwards by ReconcileReservations.
            _state.State.CashBalance -= fill.Notional;

            position ??= _state.State.Positions[fill.Symbol] = new PositionState();
            position.Quantity += fill.Quantity;
            position.CostBasis += fill.Notional;
        }
        else
        {
            _state.State.CashBalance += fill.Notional;

            position!.CostBasis -= position.CostBasis * fill.Quantity / position.Quantity;
            position.Quantity -= fill.Quantity;

            if (position.Quantity == 0)
            {
                _state.State.Positions.Remove(fill.Symbol);
            }
        }

        _state.State.Trades.Insert(0, new Trade
        {
            TradeId = fill.FillId,
            AccountId = fill.AccountId,
            Symbol = fill.Symbol,
            Side = fill.Side,
            Quantity = fill.Quantity,
            Price = fill.Price,
            ExecutedAt = fill.ExecutedAt,
        });

        if (_state.State.Trades.Count > MaxTrades)
        {
            _state.State.Trades.RemoveRange(MaxTrades, _state.State.Trades.Count - MaxTrades);
        }
    }

    private void ReleaseReservation(RestingOrder order, int quantity)
    {
        if (order.Side == OrderSide.Buy)
        {
            _state.State.ReservedCash = Math.Max(0, _state.State.ReservedCash - quantity * order.LimitPrice);
            return;
        }

        if (_state.State.Positions.TryGetValue(order.Symbol, out var position))
        {
            position.Reserved = Math.Max(0, position.Reserved - quantity);
        }
    }

    private Trade ToTrade(RestingOrder order, IReadOnlyList<Fill> fills, DateTimeOffset now)
    {
        var mine = fills.Where(f => f.OrderId == order.OrderId).ToArray();
        var filled = mine.Sum(f => f.Quantity);

        return new Trade
        {
            TradeId = order.OrderId,
            AccountId = order.AccountId,
            Symbol = order.Symbol,
            Side = order.Side,
            Quantity = filled,
            Price = filled > 0 ? mine.Sum(f => f.Notional) / filled : order.LimitPrice,
            ExecutedAt = now,
        };
    }

    public Task<IReadOnlyList<Trade>> GetTrades() =>
        Task.FromResult<IReadOnlyList<Trade>>(_state.State.Trades.ToArray());

    // Messages cross to the browser, so they don't take the silo's locale.
    private static string Money(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture);

    private static void RequirePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }
    }

    private AccountSummary ToSummary() => new()
    {
        AccountId = this.GetPrimaryKey(),
        CashBalance = _state.State.CashBalance,
        ReservedCash = _state.State.ReservedCash,
        Holdings = _state.State.Positions
            .OrderBy(p => p.Key)
            .Select(p => new Holding
            {
                Symbol = p.Key,
                Quantity = p.Value.Quantity,
                AverageCost = p.Value.CostBasis / p.Value.Quantity,
                Reserved = p.Value.Reserved,
            })
            .ToArray(),
    };
}
