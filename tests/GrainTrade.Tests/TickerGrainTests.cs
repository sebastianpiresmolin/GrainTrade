using GrainTrade.Abstractions;

namespace GrainTrade.Tests;

[Collection(ClusterCollection.Name)]
public sealed class TickerGrainTests(GrainClusterFixture fixture)
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);

    // The clock is shared across the collection, so each test uses its own
    // symbol — otherwise one test's Advance would tick another's grain.
    private ITickerGrain Ticker(string symbol) => fixture.Grains.GetGrain<ITickerGrain>(symbol);

    [Fact]
    public async Task Seeds_a_price_on_first_activation()
    {
        var quote = await Ticker("SEED").GetQuote();

        Assert.Equal("SEED", quote.Symbol);
        Assert.True(quote.Price > 0);
        Assert.Equal(0m, quote.Change);
    }

    [Fact]
    public async Task Seed_price_is_stable_per_symbol()
    {
        var a = await Ticker("STABLE").GetQuote();
        var b = await Ticker("STABLE").GetQuote();

        Assert.Equal(a.Price, b.Price);
    }

    [Fact]
    public async Task Timer_moves_the_price()
    {
        var ticker = Ticker("MOVE");
        var before = await ticker.GetQuote();

        fixture.Clock.Advance(Tick);
        var after = await WaitForTick(ticker, before.AsOf);

        Assert.NotEqual(before.AsOf, after.AsOf);
        Assert.Equal(after.Price - before.Price, after.Change);
    }

    [Fact]
    public async Task History_grows_with_each_tick()
    {
        var ticker = Ticker("HIST");
        var before = (await ticker.GetHistory()).Count;

        fixture.Clock.Advance(Tick);
        await WaitForTick(ticker, (await ticker.GetQuote()).AsOf, expectChange: false);

        Assert.True((await ticker.GetHistory()).Count > before);
    }

    [Fact]
    public async Task Price_never_goes_negative()
    {
        var ticker = Ticker("FLOOR");

        for (var i = 0; i < 50; i++)
        {
            fixture.Clock.Advance(Tick);
        }

        var history = await ticker.GetHistory();
        Assert.All(history, p => Assert.True(p.Price > 0));
    }

    // The timer callback runs asynchronously after Advance returns, so poll
    // briefly rather than assuming it has already fired.
    private static async Task<TickerQuote> WaitForTick(
        ITickerGrain ticker, DateTimeOffset since, bool expectChange = true)
    {
        for (var i = 0; i < 50; i++)
        {
            var quote = await ticker.GetQuote();
            if (quote.AsOf != since)
            {
                return quote;
            }
            await Task.Delay(20);
        }

        return expectChange
            ? throw new TimeoutException("Timer did not fire after advancing the clock.")
            : await ticker.GetQuote();
    }
}
