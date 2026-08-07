using System.Data.Common;
using System.Text.Json.Serialization;
using GrainTrade.Abstractions;
using Npgsql;
using GrainTrade.ApiHost;
using GrainTrade.ApiHost.Endpoints;
using GrainTrade.ApiHost.Streaming;

var builder = WebApplication.CreateBuilder(args);

// Clustering has to match the silo's: both sides find each other through the
// same membership table, or neither finds anything.
var postgres = builder.Configuration.GetConnectionString("Orleans");

if (!string.IsNullOrWhiteSpace(postgres))
{
    DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
}

// Orleans client, not a silo: reaches grains through their interfaces only.
builder.UseOrleansClient(client =>
{
    if (string.IsNullOrWhiteSpace(postgres))
    {
        client.UseLocalhostClustering();
    }
    else
    {
        client.UseAdoNetClustering(options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });
    }

    // Must match the provider name the silo registers.
    client.AddMemoryStreams(StreamConstants.Provider);
});

// Enums cross the wire as names ("Buy"), not ordinals — the TypeScript DTOs
// mirror the C# names, and a reordered enum shouldn't silently change meaning.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<MarketFeed>();
builder.Services.AddHostedService<MarketFeedSubscriber>();

// Synthetic liquidity so limit orders have a counterparty and the book shows depth.
builder.Services.AddHostedService<MarketMaker>();

// Where the SvelteKit host runs. Deploying without setting this is the classic
// "works locally, blocked in the browser" failure, so it's configuration.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];

const string WebCors = "web-cors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCors, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(WebCors);
app.MapAccountEndpoints();
app.MapTickerEndpoints();
app.MapStreamEndpoints();

app.Run();
