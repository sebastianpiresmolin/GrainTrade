using System.Text.Json;
using GrainTrade.ApiHost.Streaming;

namespace GrainTrade.ApiHost.Endpoints;

public static class StreamEndpoints
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

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
            // Send what we already know so a new tab isn't blank until the
            // next tick.
            foreach (var quote in feed.Latest)
            {
                await WriteEvent(http, quote, cancellationToken);
            }
            await http.Response.Body.FlushAsync(cancellationToken);

            await foreach (var quote in reader.ReadAllAsync(cancellationToken))
            {
                await WriteEvent(http, quote, cancellationToken);
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

    private static async Task WriteEvent(HttpContext http, object payload, CancellationToken ct)
    {
        await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(payload, Json)}\n\n", ct);
    }
}
