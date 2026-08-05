using System.Text.Json;
using System.Text.Json.Serialization;
using GrainTrade.Abstractions;
using GrainTrade.ApiHost.Streaming;

namespace GrainTrade.ApiHost.Endpoints;

public static class StreamEndpoints
{
    // Matches the host-wide options so enums serialise the same way here.
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly TimeSpan AccountInterval = TimeSpan.FromSeconds(1.5);

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/market/stream", StreamMarket);
        return app;
    }

    // Market data (quotes/depth/trades) is global and fans out to everyone. The
    // account is per-connection: the ?account= id (the logged-in user, tagged on
    // by the SvelteKit proxy) is settled on an interval and pushed on this one
    // stream, so each user sees their own holdings update live.
    private static async Task StreamMarket(
        HttpContext http, MarketFeed feed, IClusterClient client, CancellationToken ct)
    {
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        // Without this a reverse proxy may buffer the stream into uselessness.
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var account = Guid.TryParse(http.Request.Query["account"], out var accountId)
            ? client.GetGrain<IAccountGrain>(accountId)
            : null;

        var (id, reader) = feed.Subscribe();
        var lastAccount = "";

        try
        {
            // Replay current prices and depth, plus this account's state, so a new
            // tab isn't blank until the next change.
            foreach (var ev in feed.Snapshot())
            {
                await WriteEvent(http, ev, ct);
            }
            if (account is not null)
            {
                lastAccount = await PushAccount(http, account, lastAccount, ct);
            }
            await http.Response.Body.FlushAsync(ct);

            using var accountTimer = new PeriodicTimer(AccountInterval);
            var next = reader.ReadAsync(ct).AsTask();
            var tick = accountTimer.WaitForNextTickAsync(ct).AsTask();

            while (!ct.IsCancellationRequested)
            {
                var done = await Task.WhenAny(next, tick);
                if (done == next)
                {
                    await WriteEvent(http, await next, ct);
                    await http.Response.Body.FlushAsync(ct);
                    next = reader.ReadAsync(ct).AsTask();
                }
                else
                {
                    await tick;
                    if (account is not null)
                    {
                        lastAccount = await PushAccount(http, account, lastAccount, ct);
                    }
                    tick = accountTimer.WaitForNextTickAsync(ct).AsTask();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Browser navigated away or closed the tab — expected.
        }
        finally
        {
            feed.Unsubscribe(id);
        }
    }

    // Settle (claim any background fills) and push if the state changed. Returns
    // the new signature to compare against next time.
    private static async Task<string> PushAccount(
        HttpContext http, IAccountGrain account, string last, CancellationToken ct)
    {
        var update = new AccountUpdate(await account.Settle(), await account.GetOpenOrders());
        var json = JsonSerializer.Serialize(update, Json);
        if (json != last)
        {
            await http.Response.WriteAsync($"event: account\ndata: {json}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
        return json;
    }

    private static async Task WriteEvent(HttpContext http, MarketEvent ev, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(ev.Payload, Json);
        await http.Response.WriteAsync($"event: {ev.Event}\ndata: {data}\n\n", ct);
    }
}
