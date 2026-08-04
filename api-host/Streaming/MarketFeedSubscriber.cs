using GrainTrade.Abstractions;
using Orleans.Streams;

namespace GrainTrade.ApiHost.Streaming;

// Owns the process-wide Orleans subscriptions: one per symbol, established at
// startup and held for the host's lifetime.
public sealed class MarketFeedSubscriber(
    IClusterClient client,
    MarketFeed feed,
    ILogger<MarketFeedSubscriber> logger) : IHostedService
{
    private readonly List<StreamSubscriptionHandle<TickerQuote>> _handles = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = client.GetStreamProvider(StreamConstants.Provider);

        foreach (var symbol in MarketSymbols.All)
        {
            var stream = provider.GetStream<TickerQuote>(StreamConstants.TickerNamespace, symbol);
            _handles.Add(await stream.SubscribeAsync(OnQuote));

            // Grains only tick while activated, and a stream subscription alone
            // doesn't activate one — so wake each ticker up.
            await client.GetGrain<ITickerGrain>(symbol).GetQuote();
        }

        logger.LogInformation("Subscribed to {Count} ticker streams.", _handles.Count);
    }

    private Task OnQuote(TickerQuote quote, StreamSequenceToken? token)
    {
        feed.Publish(quote);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var handle in _handles)
        {
            await handle.UnsubscribeAsync();
        }
    }
}
