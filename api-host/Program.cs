using GrainTrade.Abstractions;
using GrainTrade.ApiHost.Endpoints;
using GrainTrade.ApiHost.Streaming;

var builder = WebApplication.CreateBuilder(args);

// Orleans client, not a silo: reaches grains through their interfaces only.
builder.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
    // Must match the provider name the silo registers.
    client.AddMemoryStreams(StreamConstants.Provider);
});

builder.Services.AddSingleton<MarketFeed>();
builder.Services.AddHostedService<MarketFeedSubscriber>();

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
app.MapAccountEndpoints();
app.MapTickerEndpoints();
app.MapStreamEndpoints();

app.Run();
