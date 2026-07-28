using GrainTrade.Abstractions;
using GrainTrade.ApiHost.Contracts;

namespace GrainTrade.ApiHost.Handlers;

// Account endpoint logic. Dependencies (Orleans client, route id, request body)
// are supplied by minimal-API DI / model binding from the endpoint mapping.
public static class AccountHandlers
{
    public static async Task<IResult> GetSummary(Guid id, IClusterClient client)
    {
        var account = client.GetGrain<IAccountGrain>(id);
        return Results.Ok(await account.GetSummary());
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

    // Map the grain's domain exceptions to 400s.
    private static async Task<IResult> Invoke(Func<Task<AccountSummary>> op)
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
