using System.Text.Json;
using System.Text.Json.Serialization;
using GrainTrade.ApiHost.Streaming;

namespace GrainTrade.ApiHost.Endpoints;

public static class StreamEndpoints
{
    // Matches the host-wide options so enums serialise the same way here.
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/market/stream", StreamMarket);
        return app;
    }

    private static async Task StreamMarket(
        HttpContext http, MarketFeed feed, CancellationToken cancellationToken)
    {
        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        // Without this a reverse proxy may buffer the stream into uselessness.
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var (id, reader) = feed.Subscribe();

        try
        {
            // Replay current prices and depth so a new tab isn't blank until the
            // next change.
            foreach (var ev in feed.Snapshot())
            {
                await WriteEvent(http, ev, cancellationToken);
            }
            await http.Response.Body.FlushAsync(cancellationToken);

            await foreach (var ev in reader.ReadAllAsync(cancellationToken))
            {
                await WriteEvent(http, ev, cancellationToken);
                await http.Response.Body.FlushAsync(cancellationToken);
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

    private static async Task WriteEvent(HttpContext http, MarketEvent ev, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(ev.Payload, Json);
        await http.Response.WriteAsync($"event: {ev.Event}\ndata: {data}\n\n", ct);
    }
}
