using GrainTrade.Abstractions;
using Orleans.Streams;

namespace GrainTrade.ApiHost.Streaming;

// Owns the process-wide Orleans subscriptions: for every symbol, its ticker,
// depth, and trade streams, established at startup and held for the host's
// lifetime.
public sealed class MarketFeedSubscriber(
    IClusterClient client,
    MarketFeed feed,
    ILogger<MarketFeedSubscriber> logger) : IHostedService
{
    // Handles across the three stream types have different generic arguments and
    // no shared non-generic base here, so keep their unsubscribe actions instead.
    private readonly List<Func<Task>> _unsubscribes = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = client.GetStreamProvider(StreamConstants.Provider);

        foreach (var symbol in MarketSymbols.All)
        {
            var quotes = provider.GetStream<TickerQuote>(StreamConstants.TickerNamespace, symbol);
            var quoteHandle = await quotes.SubscribeAsync((quote, _) =>
            {
                feed.PublishQuote(quote);
                return Task.CompletedTask;
            });
            _unsubscribes.Add(quoteHandle.UnsubscribeAsync);

            // symbol is captured per-iteration; the book stream carries no symbol,
            // so the host tags it here.
            var depth = provider.GetStream<BookDepth>(StreamConstants.DepthNamespace, symbol);
            var depthHandle = await depth.SubscribeAsync((update, _) =>
            {
                feed.PublishDepth(symbol, update);
                return Task.CompletedTask;
            });
            _unsubscribes.Add(depthHandle.UnsubscribeAsync);

            var trades = provider.GetStream<Trade>(StreamConstants.TradeNamespace, symbol);
            var tradeHandle = await trades.SubscribeAsync((trade, _) =>
            {
                feed.PublishTrade(trade);
                return Task.CompletedTask;
            });
            _unsubscribes.Add(tradeHandle.UnsubscribeAsync);

            // A ticker only ticks while activated, and subscribing doesn't
            // activate it — so wake each one. Books don't need waking: they
            // publish only when an order changes them, which activates them.
            await client.GetGrain<ITickerGrain>(symbol).GetQuote();
        }

        logger.LogInformation("Subscribed to {Count} market streams.", _unsubscribes.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var unsubscribe in _unsubscribes)
        {
            await unsubscribe();
        }
    }
}
