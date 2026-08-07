using System.Data.Common;
using GrainTrade.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

// Grains take TimeProvider rather than reading DateTime.UtcNow, so tests can
// drive their timers deterministically.
builder.Services.AddSingleton(TimeProvider.System);

// No connection string configured → run entirely in memory, so the repo still
// starts without a database. Set it via user-secrets to go durable:
//   dotnet user-secrets set ConnectionStrings:Orleans "..." --project silo/GrainTrade.Silo
var postgres = builder.Configuration.GetConnectionString("Orleans");
var durable = !string.IsNullOrWhiteSpace(postgres);

if (durable)
{
    // Orleans resolves the "Npgsql" invariant through DbProviderFactories.
    DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
}

builder.UseOrleans(silo =>
{
    if (durable)
    {
        silo.UseAdoNetClustering(options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });

        // Money and executed trades: state a restart must not lose.
        silo.AddAdoNetGrainStorage("accounts", options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });
        silo.AddAdoNetGrainStorage("orderbooks", options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });

        // Order expiry must fire even if nothing else wakes the book, and
        // survive a restart — so reminders, persisted alongside the state.
        silo.UseAdoNetReminderService(options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });
        silo.AddAdoNetGrainStorage("watchlists", options =>
        {
            options.Invariant = "Npgsql";
            options.ConnectionString = postgres;
        });
    }
    else
    {
        silo.UseLocalhostClustering();
        silo.AddMemoryGrainStorage("accounts");
        silo.AddMemoryGrainStorage("orderbooks");
        silo.UseInMemoryReminderService();
        silo.AddMemoryGrainStorage("watchlists");
    }

    // Prices stay in memory whichever mode we're in: a simulated price is
    // meaningless after a restart and reseeds deterministically per symbol.
    silo.AddMemoryGrainStorage("tickers");

    // In-memory streams: no external broker, lost on restart. Fine for price
    // ticks, which are worthless a second later anyway.
    silo.AddMemoryStreams(StreamConstants.Provider);
    // Backing store the memory stream provider needs for its queue metadata.
    silo.AddMemoryGrainStorage("PubSubStore");
});

var host = builder.Build();
host.Run();
