using GrainTrade.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Orleans client, not a silo: reaches grains through their interfaces only
// (references Abstractions, never Grains).
builder.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
});

const string DevCors = "dev-cors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCors, policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors(DevCors);

app.MapGet("/accounts/{id:guid}", async (Guid id, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    return Results.Ok(await account.GetSummary());
});

app.MapPost("/accounts/{id:guid}/deposit", async (Guid id, AmountRequest req, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    return await Invoke(() => account.Deposit(req.Amount));
});

app.MapPost("/accounts/{id:guid}/withdraw", async (Guid id, AmountRequest req, IClusterClient client) =>
{
    var account = client.GetGrain<IAccountGrain>(id);
    return await Invoke(() => account.Withdraw(req.Amount));
});

app.Run();

// Map the grain's domain exceptions to 400s.
static async Task<IResult> Invoke(Func<Task<AccountSummary>> op)
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

record AmountRequest(decimal Amount);
