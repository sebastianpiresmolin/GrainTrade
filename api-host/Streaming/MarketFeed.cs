using System.Collections.Concurrent;
using System.Threading.Channels;
using GrainTrade.Abstractions;

namespace GrainTrade.ApiHost.Streaming;

// One SSE payload. Event is the SSE event name the browser listens for; Payload
// is serialised as its data.
public readonly record struct MarketEvent(string Event, object Payload);

// BookDepth carries no symbol, but the browser needs to know which book changed,
// so the host tags it on the way out.
public sealed record DepthUpdate(string Symbol, IReadOnlyList<DepthLevel> Bids, IReadOnlyList<DepthLevel> Asks);

// Bridges Orleans streams to connected browsers. Subscribes to each grain stream
// once for the whole process and fans out to every SSE client, so N browsers cost
// one set of Orleans subscriptions rather than N.
//
// Concurrent collections are correct here — unlike inside a grain, this is
// ordinary shared state in a multi-threaded host with no turn-based guarantee.
public sealed class MarketFeed
{
    // Latest state per symbol, replayed to a client the moment it connects so a
    // new tab isn't blank until the next change. Trades are events, not state,
    // so they aren't cached — the page loads its initial tape over REST.
    private readonly ConcurrentDictionary<string, TickerQuote> _quotes = new();
    private readonly ConcurrentDictionary<string, DepthUpdate> _depth = new();
    private readonly ConcurrentDictionary<Guid, Channel<MarketEvent>> _subscribers = new();

    public IReadOnlyCollection<MarketEvent> Snapshot()
    {
        var events = new List<MarketEvent>(_quotes.Count + _depth.Count);
        events.AddRange(_quotes.Values.Select(q => new MarketEvent("quote", q)));
        events.AddRange(_depth.Values.Select(d => new MarketEvent("depth", d)));
        return events;
    }

    public void PublishQuote(TickerQuote quote)
    {
        _quotes[quote.Symbol] = quote;
        Fan(new MarketEvent("quote", quote));
    }

    public void PublishDepth(string symbol, BookDepth depth)
    {
        var update = new DepthUpdate(symbol, depth.Bids, depth.Asks);
        _depth[symbol] = update;
        Fan(new MarketEvent("depth", update));
    }

    public void PublishTrade(Trade trade) => Fan(new MarketEvent("trade", trade));

    private void Fan(MarketEvent ev)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            // Bounded + DropOldest: a slow browser falls behind rather than
            // growing an unbounded buffer.
            subscriber.Writer.TryWrite(ev);
        }
    }

    public (Guid Id, ChannelReader<MarketEvent> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<MarketEvent>(
            new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });

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
