using GrainTrade.Abstractions;

namespace GrainTrade.Tests;

[Collection(ClusterCollection.Name)]
public sealed class OrderTests(GrainClusterFixture fixture)
{
    private IAccountGrain NewAccount() => fixture.Grains.GetGrain<IAccountGrain>(Guid.NewGuid());

    private async Task<IAccountGrain> FundedAccount(decimal cash)
    {
        var account = NewAccount();
        await account.Deposit(cash);
        return account;
    }

    // Each test uses its own symbol so a concurrent test's trades don't land in
    // the same order book.
    private async Task<decimal> PriceOf(string symbol) =>
        (await fixture.Grains.GetGrain<ITickerGrain>(symbol).GetQuote()).Price;

    [Fact]
    public async Task Buy_moves_cash_into_a_holding()
    {
        var account = await FundedAccount(100_000m);
        var price = await PriceOf("BUY1");

        var result = await account.PlaceOrder("BUY1", OrderSide.Buy, 10);

        Assert.Equal(10, result.Trade.Quantity);
        Assert.Equal(OrderSide.Buy, result.Trade.Side);

        var holding = Assert.Single(result.Account.Holdings);
        Assert.Equal("BUY1", holding.Symbol);
        Assert.Equal(10, holding.Quantity);
        Assert.Equal(100_000m - result.Trade.Notional, result.Account.CashBalance);
        Assert.Equal(price, holding.AverageCost);
    }

    [Fact]
    public async Task Sell_returns_cash_and_reduces_the_holding()
    {
        var account = await FundedAccount(100_000m);
        await account.PlaceOrder("SELL1", OrderSide.Buy, 10);

        var result = await account.PlaceOrder("SELL1", OrderSide.Sell, 4);

        var holding = Assert.Single(result.Account.Holdings);
        Assert.Equal(6, holding.Quantity);
    }

    [Fact]
    public async Task Selling_the_whole_position_removes_it()
    {
        var account = await FundedAccount(100_000m);
        await account.PlaceOrder("SELL2", OrderSide.Buy, 5);

        var result = await account.PlaceOrder("SELL2", OrderSide.Sell, 5);

        Assert.Empty(result.Account.Holdings);
    }

    [Fact]
    public async Task Buying_beyond_the_balance_is_rejected()
    {
        var account = await FundedAccount(50m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => account.PlaceOrder("POOR", OrderSide.Buy, 1_000));

        var summary = await account.GetSummary();
        Assert.Equal(50m, summary.CashBalance);
        Assert.Empty(summary.Holdings);
    }

    [Fact]
    public async Task Selling_shares_you_do_not_hold_is_rejected()
    {
        var account = await FundedAccount(100_000m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => account.PlaceOrder("NONE", OrderSide.Sell, 1));

        Assert.Equal(100_000m, (await account.GetSummary()).CashBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_quantities_are_rejected(int quantity)
    {
        var account = await FundedAccount(100_000m);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => account.PlaceOrder("QTY", OrderSide.Buy, quantity));
    }

    [Fact]
    public async Task Trades_are_recorded_on_the_account_and_the_book()
    {
        var account = await FundedAccount(100_000m);

        var result = await account.PlaceOrder("BOOK1", OrderSide.Buy, 3);

        var accountTrades = await account.GetTrades();
        Assert.Contains(accountTrades, t => t.TradeId == result.Trade.TradeId);

        var bookTrades = await fixture.Grains.GetGrain<IOrderBookGrain>("BOOK1").GetRecentTrades();
        Assert.Contains(bookTrades, t => t.TradeId == result.Trade.TradeId);
    }

    // The point of routing orders through the account grain: concurrent buys
    // can't overspend, because the grain handles them one at a time.
    [Fact]
    public async Task Concurrent_buys_cannot_overspend()
    {
        var price = await PriceOf("RACE1");
        // Enough for exactly 3 lots of 10.
        var account = await FundedAccount(price * 30);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(async _ =>
            {
                try
                {
                    await account.PlaceOrder("RACE1", OrderSide.Buy, 10);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }));

        var summary = await account.GetSummary();
        var filled = results.Count(ok => ok);

        Assert.True(summary.CashBalance >= 0, $"Balance went negative: {summary.CashBalance}");
        Assert.Equal(filled * 10, summary.Holdings.Single().Quantity);
    }

    // Same guarantee on the sell side: shares can't be sold twice.
    [Fact]
    public async Task Concurrent_sells_cannot_oversell()
    {
        var account = await FundedAccount(1_000_000m);
        await account.PlaceOrder("RACE2", OrderSide.Buy, 10);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(async _ =>
            {
                try
                {
                    await account.PlaceOrder("RACE2", OrderSide.Sell, 4);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }));

        var summary = await account.GetSummary();
        var sold = results.Count(ok => ok) * 4;

        Assert.Equal(2, results.Count(ok => ok));
        Assert.Equal(10 - sold, summary.Holdings.SingleOrDefault()?.Quantity ?? 0);
    }
}
