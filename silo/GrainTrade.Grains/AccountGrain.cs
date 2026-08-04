using System.Globalization;
using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;

namespace GrainTrade.Grains;

public sealed class AccountGrain : Grain, IAccountGrain
{
    private const int MaxTrades = 100;

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

        if (side == OrderSide.Buy)
        {
            if (notional > _state.State.CashBalance)
            {
                throw new InvalidOperationException(
                    $"Insufficient funds: {quantity} {symbol} at {Money(price)} costs {Money(notional)}, balance is {Money(_state.State.CashBalance)}.");
            }
        }
        else if (position is null || position.Quantity < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient shares: cannot sell {quantity} {symbol}, holding {position?.Quantity ?? 0}.");
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
        Holdings = _state.State.Positions
            .OrderBy(p => p.Key)
            .Select(p => new Holding
            {
                Symbol = p.Key,
                Quantity = p.Value.Quantity,
                AverageCost = p.Value.CostBasis / p.Value.Quantity,
            })
            .ToArray(),
    };
}
