using GrainTrade.Abstractions;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Grains take TimeProvider rather than reading DateTime.UtcNow, so tests can
// drive their timers deterministically.
builder.Services.AddSingleton(TimeProvider.System);

builder.UseOrleans(silo =>
{
    silo.UseLocalhostClustering();

    // In-memory storage: state is lost on restart. Swapping for a durable
    // provider (Postgres) later is a change here only — the grain doesn't move.
    silo.AddMemoryGrainStorage("accounts");
    silo.AddMemoryGrainStorage("tickers");
    silo.AddMemoryGrainStorage("orderbooks");

    // In-memory streams: no external broker, lost on restart. Fine for price
    // ticks, which are worthless a second later anyway.
    silo.AddMemoryStreams(StreamConstants.Provider);
    // Backing store the memory stream provider needs for its queue metadata.
    silo.AddMemoryGrainStorage("PubSubStore");
});

var host = builder.Build();
host.Run();
