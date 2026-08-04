using GrainTrade.Abstractions;

namespace GrainTrade.ApiHost.Handlers;

public static class TickerHandlers
{
    // Fan out to every ticker grain in parallel. Calls go to independent grains,
    // so there's no cycle and no shared state to contend on.
    public static async Task<IResult> GetMarket(IClusterClient client)
    {
        var quotes = await Task.WhenAll(
            MarketSymbols.All.Select(s => client.GetGrain<ITickerGrain>(s).GetQuote()));

        return Results.Ok(quotes);
    }

    public static async Task<IResult> GetQuote(string symbol, IClusterClient client)
    {
        if (!MarketSymbols.IsKnown(symbol))
        {
            return Results.NotFound();
        }

        return Results.Ok(await client.GetGrain<ITickerGrain>(symbol).GetQuote());
    }

    public static async Task<IResult> GetTrades(string symbol, IClusterClient client)
    {
        if (!MarketSymbols.IsKnown(symbol))
        {
            return Results.NotFound();
        }

        return Results.Ok(await client.GetGrain<IOrderBookGrain>(symbol).GetRecentTrades());
    }

    public static async Task<IResult> GetHistory(string symbol, IClusterClient client)
    {
        if (!MarketSymbols.IsKnown(symbol))
        {
            return Results.NotFound();
        }

        return Results.Ok(await client.GetGrain<ITickerGrain>(symbol).GetHistory());
    }
}
