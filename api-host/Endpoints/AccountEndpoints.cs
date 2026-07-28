using GrainTrade.ApiHost.Handlers;

namespace GrainTrade.ApiHost.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts");

        accounts.MapGet("/{id:guid}", AccountHandlers.GetSummary);
        accounts.MapPost("/{id:guid}/deposit", AccountHandlers.Deposit);
        accounts.MapPost("/{id:guid}/withdraw", AccountHandlers.Withdraw);

        return app;
    }
}
