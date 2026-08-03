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
});

var host = builder.Build();
host.Run();
