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

        // Our own orders are gone now, so the book shows only other participants.
        // Quote passively: keep our best bid strictly below the best resting ask
        // and our best ask strictly above the best resting bid, so we never
        // aggress into — and sweep — someone else's resting order.
        var rest = await book.GetDepth();
        decimal? bestBid = rest.Bids.Count > 0 ? rest.Bids[0].Price : null;
        decimal? bestAsk = rest.Asks.Count > 0 ? rest.Asks[0].Price : null;

        var now = DateTimeOffset.UtcNow;
        var basis = decimal.Round(mid, 2);
        var gap = Math.Max(0.01m, decimal.Round(mid * Step, 2));

        var bidTop = basis - gap;
        if (bestAsk is decimal ask && bidTop >= ask)
        {
            bidTop = ask - 0.01m;
        }
        var askBottom = basis + gap;
        if (bestBid is decimal bid && askBottom <= bid)
        {
            askBottom = bid + 0.01m;
        }

        for (var k = 0; k < Levels; k++)
        {
            var qty = 100 * (k + 2);

            var bidPrice = bidTop - gap * k;
            if (bidPrice > 0)
            {
                await book.PlaceLimit(Order(symbol, OrderSide.Buy, bidPrice, qty, now));
            }
            await book.PlaceLimit(Order(symbol, OrderSide.Sell, askBottom + gap * k, qty, now));
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
