using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(silo =>
{
    silo.UseLocalhostClustering();

    // In-memory storage: state is lost on restart. Swapping for a durable
    // provider (Postgres) later is a change here only — the grain doesn't move.
    silo.AddMemoryGrainStorage("accounts");
});

var host = builder.Build();
host.Run();
