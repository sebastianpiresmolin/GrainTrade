using System.Collections.Concurrent;
using System.Threading.Channels;
using GrainTrade.Abstractions;

namespace GrainTrade.ApiHost.Streaming;

// Bridges Orleans streams to connected browsers. Subscribes to each ticker
// stream once for the whole process and fans out to every SSE client, so N
// browsers cost one set of Orleans subscriptions rather than N.
//
// Concurrent collections are correct here — unlike inside a grain, this is
// ordinary shared state in a multi-threaded host with no turn-based guarantee.
public sealed class MarketFeed
{
    private readonly ConcurrentDictionary<string, TickerQuote> _latest = new();
    private readonly ConcurrentDictionary<Guid, Channel<TickerQuote>> _subscribers = new();

    public IReadOnlyCollection<TickerQuote> Latest => _latest.Values.ToArray();

    // Called by the stream observer when a grain publishes a tick.
    public void Publish(TickerQuote quote)
    {
        _latest[quote.Symbol] = quote;

        foreach (var subscriber in _subscribers.Values)
        {
            // Bounded + DropOldest: a slow browser falls behind rather than
            // growing an unbounded buffer. Stale prices are worthless anyway.
            subscriber.Writer.TryWrite(quote);
        }
    }

    public (Guid Id, ChannelReader<TickerQuote> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<TickerQuote>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });

        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
