using GrainTrade.ApiHost.Handlers;

namespace GrainTrade.ApiHost.Endpoints;

public static class TickerEndpoints
{
    public static IEndpointRouteBuilder MapTickerEndpoints(this IEndpointRouteBuilder app)
    {
        var market = app.MapGroup("/market");

        market.MapGet("/", TickerHandlers.GetMarket);
        market.MapGet("/{symbol}", TickerHandlers.GetQuote);
        market.MapGet("/{symbol}/history", TickerHandlers.GetHistory);
        market.MapGet("/{symbol}/trades", TickerHandlers.GetTrades);

        return app;
    }
}
