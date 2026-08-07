using GrainTrade.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;

namespace GrainTrade.ApiHost;

// A synthetic market maker: rests a small ladder of bids and asks around each
// ticker's price so limit orders always have something to fill against and the
// book shows depth. It's a plain host service — no identity, no persisted state,
// no turn-based concurrency to justify a grain (per project conventions).
//
// Its orders carry a fixed synthetic account id; no AccountGrain exists for it,
// and its fills are drained and discarded (it has infinite inventory).
public sealed class MarketMaker(IClusterClient client, ILogger<MarketMaker> logger) : BackgroundService
{
    private static readonly Guid MakerId = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private const int Levels = 3;
    private const decimal Step = 0.0006m; // gap between levels, as a fraction of mid
    private const decimal RequoteMove = 0.0015m; // re-quote once mid drifts past this

    // Last mid we quoted around, per symbol. One instance, one loop — a plain
    // dictionary needs no synchronisation.
    private readonly Dictionary<string, decimal> _lastMid = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the client connect and the cluster settle before quoting.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (await SafeWait(timer, stoppingToken))
        {
            foreach (var symbol in MarketSymbols.All)
            {
                try
                {
                    await Quote(symbol);
                }
                catch (Exception ex)
                {
                    // Early ticks can race the cluster coming up; keep going.
                    logger.LogDebug(ex, "Market maker skipped {Symbol}", symbol);
                }
            }
        }
    }

    private static async Task<bool> SafeWait(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task Quote(string symbol)
    {
        var book = client.GetGrain<IOrderBookGrain>(symbol);

        // Drain our own fills so the book's unclaimed list stays bounded.
        await book.ClaimFills(MakerId);

        var mid = (await client.GetGrain<ITickerGrain>(symbol).GetQuote()).Price;
        if (mid <= 0)
        {
            return;
        }

        var open = await book.GetOpenOrders(MakerId);
        var drifted = !_lastMid.TryGetValue(symbol, out var last)
            || Math.Abs(mid - last) > last * RequoteMove;

        // Leave stable quotes in place; re-quote only when the price moved or our
        // quotes are gone, so the book follows the price without flickering every tick.
        if (open.Count > 0 && !drifted)
        {
            return;
        }

        foreach (var order in open)
        {
            await book.Cancel(order.OrderId, MakerId);
        }

        // Quote a ladder around the mid. These go through the matching engine, so
        // they lift any resting ask below the mid and hit any resting bid above it
        // — orders the market has crossed fill, as a limit order should. Orders
        // priced away from the market (a sell above the mid, a buy below it) rest
        // untouched until the price reaches them.
        var now = DateTimeOffset.UtcNow;
        var basis = decimal.Round(mid, 2);
        for (var k = 1; k <= Levels; k++)
        {
            var offset = decimal.Round(mid * Step * k, 2);
            var qty = 100 * (k + 1);

            var bid = basis - offset;
            if (bid > 0)
            {
                await book.PlaceLimit(Order(symbol, OrderSide.Buy, bid, qty, now));
            }
            await book.PlaceLimit(Order(symbol, OrderSide.Sell, basis + offset, qty, now));
        }

        _lastMid[symbol] = mid;
    }

    private static RestingOrder Order(string symbol, OrderSide side, decimal price, int qty, DateTimeOffset now) =>
        new()
        {
            OrderId = Guid.NewGuid(),
            AccountId = MakerId,
            Symbol = symbol,
            Side = side,
            LimitPrice = price,
            Quantity = qty,
            Remaining = qty,
            PlacedAt = now,
            ExpiresAt = now.AddHours(1),
        };
}
