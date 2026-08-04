using GrainTrade.Abstractions;

namespace GrainTrade.Tests;

[Collection(ClusterCollection.Name)]
public sealed class LimitOrderTests(GrainClusterFixture fixture)
{
    private async Task<IAccountGrain> Funded(decimal cash = 1_000_000m)
    {
        var account = fixture.Grains.GetGrain<IAccountGrain>(Guid.NewGuid());
        await account.Deposit(cash);
        return account;
    }

    // Gives an account shares to sell, without going through the book.
    private async Task<IAccountGrain> Holding(string symbol, int quantity)
    {
        var account = await Funded();
        await account.PlaceOrder(symbol, OrderSide.Buy, quantity);
        return account;
    }

    [Fact]
    public async Task Unmatched_order_rests_and_reserves_cash()
    {
        var account = await Funded(10_000m);

        var result = await account.PlaceLimitOrder("REST1", OrderSide.Buy, 10, 50m);

        Assert.Equal(0, result.Trade.Quantity);
        Assert.Equal(500m, result.Account.ReservedCash);
        Assert.Equal(9_500m, result.Account.AvailableCash);
        // Reserved, not spent.
        Assert.Equal(10_000m, result.Account.CashBalance);
    }

    [Fact]
    public async Task Crossing_orders_match()
    {
        var seller = await Holding("MATCH1", 10);
        var buyer = await Funded();

        await seller.PlaceLimitOrder("MATCH1", OrderSide.Sell, 10, 50m);
        var result = await buyer.PlaceLimitOrder("MATCH1", OrderSide.Buy, 10, 55m);

        Assert.Equal(10, result.Trade.Quantity);
        // Resting order's price wins — the buyer improves on their own limit.
        Assert.Equal(50m, result.Trade.Price);

        var holding = result.Account.Holdings.Single(h => h.Symbol == "MATCH1");
        Assert.Equal(10, holding.Quantity);
    }

    [Fact]
    public async Task Non_crossing_orders_both_rest()
    {
        var seller = await Holding("SPREAD1", 5);
        var buyer = await Funded();

        await seller.PlaceLimitOrder("SPREAD1", OrderSide.Sell, 5, 60m);
        var result = await buyer.PlaceLimitOrder("SPREAD1", OrderSide.Buy, 5, 40m);

        Assert.Equal(0, result.Trade.Quantity);

        var depth = await fixture.Grains.GetGrain<IOrderBookGrain>("SPREAD1").GetDepth();
        Assert.Single(depth.Bids);
        Assert.Single(depth.Asks);
    }

    [Fact]
    public async Task Partial_fill_leaves_the_remainder_resting()
    {
        var seller = await Holding("PARTIAL1", 4);
        var buyer = await Funded();

        await seller.PlaceLimitOrder("PARTIAL1", OrderSide.Sell, 4, 50m);
        var result = await buyer.PlaceLimitOrder("PARTIAL1", OrderSide.Buy, 10, 50m);

        Assert.Equal(4, result.Trade.Quantity);

        // Six still wanted, resting as a bid.
        var depth = await fixture.Grains.GetGrain<IOrderBookGrain>("PARTIAL1").GetDepth();
        Assert.Equal(6, depth.Bids.Single().Quantity);
        Assert.Empty(depth.Asks);
    }

    [Fact]
    public async Task Fill_sweeps_several_price_levels_best_first()
    {
        var cheap = await Holding("SWEEP1", 3);
        var dear = await Holding("SWEEP1", 3);
        var buyer = await Funded();

        await dear.PlaceLimitOrder("SWEEP1", OrderSide.Sell, 3, 52m);
        await cheap.PlaceLimitOrder("SWEEP1", OrderSide.Sell, 3, 50m);

        // Buying only 3 forces a choice: the cheap ask must go first.
        var result = await buyer.PlaceLimitOrder("SWEEP1", OrderSide.Buy, 3, 55m);

        Assert.Equal(3, result.Trade.Quantity);
        Assert.Equal(50m, result.Trade.Price);

        // The dearer ask is untouched.
        var depth = await fixture.Grains.GetGrain<IOrderBookGrain>("SWEEP1").GetDepth();
        Assert.Equal(52m, depth.Asks.Single().Price);
    }

    // Mirror of the buy case: an incoming sell should hit the highest bid.
    [Fact]
    public async Task Incoming_sell_takes_the_best_bid_first()
    {
        var low = await Funded();
        var high = await Funded();
        var seller = await Holding("SWEEP3", 3);

        await low.PlaceLimitOrder("SWEEP3", OrderSide.Buy, 3, 50m);
        await high.PlaceLimitOrder("SWEEP3", OrderSide.Buy, 3, 52m);

        var result = await seller.PlaceLimitOrder("SWEEP3", OrderSide.Sell, 3, 45m);

        Assert.Equal(3, result.Trade.Quantity);
        Assert.Equal(52m, result.Trade.Price);

        var depth = await fixture.Grains.GetGrain<IOrderBookGrain>("SWEEP3").GetDepth();
        Assert.Equal(50m, depth.Bids.Single().Price);
    }

    [Fact]
    public async Task Fill_sweeps_across_levels_when_one_is_not_enough()
    {
        var cheap = await Holding("SWEEP2", 3);
        var dear = await Holding("SWEEP2", 3);
        var buyer = await Funded();

        await dear.PlaceLimitOrder("SWEEP2", OrderSide.Sell, 3, 52m);
        await cheap.PlaceLimitOrder("SWEEP2", OrderSide.Sell, 3, 50m);

        var result = await buyer.PlaceLimitOrder("SWEEP2", OrderSide.Buy, 6, 55m);

        Assert.Equal(6, result.Trade.Quantity);
        // 3 @ 50 + 3 @ 52 averages 51.
        Assert.Equal(51m, result.Trade.Price);
    }

    [Fact]
    public async Task Equal_prices_fill_in_arrival_order()
    {
        var first = await Holding("TIME1", 5);
        var second = await Holding("TIME1", 5);
        var buyer = await Funded();

        await first.PlaceLimitOrder("TIME1", OrderSide.Sell, 5, 50m);
        // Without advancing, both orders share a PlacedAt and the tie-break has
        // nothing to order by.
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await second.PlaceLimitOrder("TIME1", OrderSide.Sell, 5, 50m);

        // Only enough for one of them.
        await buyer.PlaceLimitOrder("TIME1", OrderSide.Buy, 5, 50m);

        // The earlier seller filled; the later one still rests.
        Assert.Empty(await first.GetOpenOrders());
        Assert.Single(await second.GetOpenOrders());
    }

    [Fact]
    public async Task Seller_is_settled_when_it_next_checks()
    {
        var seller = await Holding("SETTLE1", 8);
        var buyer = await Funded();

        var before = (await seller.Settle()).CashBalance;
        await seller.PlaceLimitOrder("SETTLE1", OrderSide.Sell, 8, 50m);
        await buyer.PlaceLimitOrder("SETTLE1", OrderSide.Buy, 8, 50m);

        var after = await seller.Settle();

        Assert.Equal(before + 400m, after.CashBalance);
        Assert.DoesNotContain(after.Holdings, h => h.Symbol == "SETTLE1");
    }

    [Fact]
    public async Task Settling_twice_does_not_double_apply()
    {
        var seller = await Holding("ONCE1", 5);
        var buyer = await Funded();

        await seller.PlaceLimitOrder("ONCE1", OrderSide.Sell, 5, 50m);
        await buyer.PlaceLimitOrder("ONCE1", OrderSide.Buy, 5, 50m);

        var first = await seller.Settle();
        var second = await seller.Settle();

        Assert.Equal(first.CashBalance, second.CashBalance);
    }

    [Fact]
    public async Task Cancelling_releases_the_reservation()
    {
        var account = await Funded(10_000m);
        await account.PlaceLimitOrder("CANCEL1", OrderSide.Buy, 10, 50m);

        var open = await account.GetOpenOrders();
        var cancelled = await account.CancelLimitOrder("CANCEL1", open.Single().OrderId);

        Assert.True(cancelled);
        var summary = await account.Settle();
        Assert.Equal(0m, summary.ReservedCash);
        Assert.Equal(10_000m, summary.AvailableCash);
    }

    [Fact]
    public async Task Cannot_cancel_another_accounts_order()
    {
        var owner = await Funded(10_000m);
        var stranger = await Funded(10_000m);
        await owner.PlaceLimitOrder("CANCEL2", OrderSide.Buy, 5, 50m);

        var order = (await owner.GetOpenOrders()).Single();

        Assert.False(await stranger.CancelLimitOrder("CANCEL2", order.OrderId));
        Assert.Single(await owner.GetOpenOrders());
    }

    // Reserved funds back a resting order, so they can't be spent again.
    [Fact]
    public async Task Reserved_cash_is_not_available_to_other_orders()
    {
        var account = await Funded(1_000m);
        await account.PlaceLimitOrder("LOCK1", OrderSide.Buy, 10, 90m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => account.PlaceLimitOrder("LOCK1", OrderSide.Buy, 10, 50m));
    }

    [Fact]
    public async Task Reserved_shares_cannot_be_sold_twice()
    {
        var account = await Holding("LOCK2", 10);
        await account.PlaceLimitOrder("LOCK2", OrderSide.Sell, 10, 50m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => account.PlaceLimitOrder("LOCK2", OrderSide.Sell, 5, 50m));
    }

    [Fact]
    public async Task Reserved_cash_is_not_spendable_by_a_market_order()
    {
        var account = await Funded(1_000m);
        await account.PlaceLimitOrder("LOCK3", OrderSide.Buy, 10, 95m);

        // 950 reserved; a market order can only use what's left.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => account.PlaceOrder("LOCK3", OrderSide.Buy, 1_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_quantity_is_rejected(int quantity)
    {
        var account = await Funded();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => account.PlaceLimitOrder("BAD1", OrderSide.Buy, quantity, 50m));
    }

    [Fact]
    public async Task Non_positive_limit_price_is_rejected()
    {
        var account = await Funded();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => account.PlaceLimitOrder("BAD2", OrderSide.Buy, 5, 0m));
    }

    [Fact]
    public async Task Depth_aggregates_quantity_per_price()
    {
        var a = await Funded();
        var b = await Funded();
        await a.PlaceLimitOrder("DEPTH1", OrderSide.Buy, 5, 40m);
        await b.PlaceLimitOrder("DEPTH1", OrderSide.Buy, 7, 40m);

        var depth = await fixture.Grains.GetGrain<IOrderBookGrain>("DEPTH1").GetDepth();

        Assert.Equal(12, depth.Bids.Single().Quantity);
    }

    // The single-grain-per-symbol guarantee: concurrent buyers competing for
    // one resting sell order can't both fill it.
    [Fact]
    public async Task Concurrent_buyers_cannot_overfill_one_resting_order()
    {
        var seller = await Holding("RACE3", 10);
        await seller.PlaceLimitOrder("RACE3", OrderSide.Sell, 10, 50m);

        var buyers = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Funded()));
        var results = await Task.WhenAll(
            buyers.Select(b => b.PlaceLimitOrder("RACE3", OrderSide.Buy, 10, 50m)));

        // Exactly one buyer got the 10 shares; the rest rest.
        Assert.Equal(1, results.Count(r => r.Trade.Quantity == 10));
        Assert.Equal(5, results.Count(r => r.Trade.Quantity == 0));
    }
}
