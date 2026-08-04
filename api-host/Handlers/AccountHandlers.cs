using GrainTrade.Abstractions;
using GrainTrade.ApiHost.Contracts;

namespace GrainTrade.ApiHost.Handlers;

// Account endpoint logic. Dependencies (Orleans client, route id, request body)
// are supplied by minimal-API DI / model binding from the endpoint mapping.
public static class AccountHandlers
{
    // Settle() rather than GetSummary(): fills land on the book, so reading
    // without claiming them would show a balance that's already out of date.
    public static async Task<IResult> GetSummary(Guid id, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Results.Ok(await account.Settle());
    }

    public static Task<IResult> Deposit(Guid id, AmountRequest req, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Invoke(() => account.Deposit(req.Amount));
    }

    public static Task<IResult> Withdraw(Guid id, AmountRequest req, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Invoke(() => account.Withdraw(req.Amount));
    }

    public static Task<IResult> PlaceOrder(Guid id, OrderRequest req, IClusterClient client)
    {
        if (!MarketSymbols.IsKnown(req.Symbol))
        {
            return Task.FromResult(Results.Problem(
                detail: $"Unknown symbol \"{req.Symbol}\".",
                statusCode: StatusCodes.Status400BadRequest));
        }

        var account = client.GetGrain<IAccountGrain>(id);
        return Invoke(() => account.PlaceOrder(req.Symbol, req.Side, req.Quantity));
    }

    public static async Task<IResult> GetTrades(Guid id, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Results.Ok(await account.GetTrades());
    }

    public static Task<IResult> PlaceLimitOrder(Guid id, LimitOrderRequest req, IClusterClient client)
    {
        if (!MarketSymbols.IsKnown(req.Symbol))
        {
            return Task.FromResult(Results.Problem(
                detail: $"Unknown symbol \"{req.Symbol}\".",
                statusCode: StatusCodes.Status400BadRequest));
        }

        var account = client.GetGrain<IAccountGrain>(id);
        return Invoke(() => account.PlaceLimitOrder(req.Symbol, req.Side, req.Quantity, req.LimitPrice));
    }

    public static async Task<IResult> CancelLimitOrder(
        Guid id, string symbol, Guid orderId, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return await account.CancelLimitOrder(symbol, orderId)
            ? Results.NoContent()
            : Results.NotFound();
    }

    public static async Task<IResult> GetOpenOrders(Guid id, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Results.Ok(await account.GetOpenOrders());
    }

    // Settling before reading means a summary never lags behind fills that
    // happened while this account was idle.
    public static async Task<IResult> Settle(Guid id, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Results.Ok(await account.Settle());
    }

    // Map the grain's domain exceptions to 400s.
    private static async Task<IResult> Invoke<T>(Func<Task<T>> op)
    {
        try
        {
            return Results.Ok(await op());
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
