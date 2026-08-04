using System.Collections.Concurrent;
using GrainTrade.Abstractions;
using Orleans.Streams;

namespace GrainTrade.Tests;

[Collection(StreamCollection.Name)]
public sealed class TickerStreamTests(StreamClusterFixture fixture)
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Tick_publishes_a_quote_to_subscribers()
    {
        const string symbol = "STREAM";
        var received = new ConcurrentQueue<TickerQuote>();

        var stream = fixture.Cluster.Client
            .GetStreamProvider(StreamConstants.Provider)
            .GetStream<TickerQuote>(StreamConstants.TickerNamespace, symbol);

        var handle = await stream.SubscribeAsync((quote, _) =>
        {
            received.Enqueue(quote);
            return Task.CompletedTask;
        });

        try
        {
            // Subscribing doesn't activate the grain — the timer only runs once
            // something wakes it up.
            await fixture.Grains.GetGrain<ITickerGrain>(symbol).GetQuote();

            var quote = await WaitFor(received);

            Assert.Equal(symbol, quote.Symbol);
            Assert.True(quote.Price > 0);
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task Each_symbol_publishes_to_its_own_stream()
    {
        const string mine = "MINE";
        const string other = "OTHER";
        var received = new ConcurrentQueue<TickerQuote>();

        var stream = fixture.Cluster.Client
            .GetStreamProvider(StreamConstants.Provider)
            .GetStream<TickerQuote>(StreamConstants.TickerNamespace, mine);

        var handle = await stream.SubscribeAsync((quote, _) =>
        {
            received.Enqueue(quote);
            return Task.CompletedTask;
        });

        try
        {
            await fixture.Grains.GetGrain<ITickerGrain>(mine).GetQuote();
            await fixture.Grains.GetGrain<ITickerGrain>(other).GetQuote();

            await WaitFor(received);

            Assert.All(received, q => Assert.Equal(mine, q.Symbol));
        }
        finally
        {
            await handle.UnsubscribeAsync();
        }
    }

    // A grain's timer is due 2s after *its* activation, and the shared clock has
    // usually moved on by then — so keep nudging it rather than assuming one
    // Advance lands. Delivery is also async after the tick, hence the poll.
    private async Task<TickerQuote> WaitFor(ConcurrentQueue<TickerQuote> queue)
    {
        for (var i = 0; i < 40; i++)
        {
            if (queue.TryPeek(out var quote))
            {
                return quote;
            }

            fixture.Clock.Advance(Tick);
            await Task.Delay(25);
        }

        throw new TimeoutException("No quote arrived on the stream.");
    }
}
