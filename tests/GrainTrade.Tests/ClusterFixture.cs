using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Orleans.TestingHost;

namespace GrainTrade.Tests;

// Shared by every test in the collection, so the fake clock is shared too —
// tests that advance it must use their own ticker symbol to stay isolated.
public static class TestClock
{
    public static readonly FakeTimeProvider Instance =
        new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
}

// Configures each in-process test silo. Mirrors Silo/Program.cs — same storage
// provider names the grains' [PersistentState] attributes ask for.
file sealed class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder silo)
    {
        silo.AddMemoryGrainStorage("accounts");
        silo.AddMemoryGrainStorage("tickers");

        silo.ConfigureServices(services =>
        {
            // Injected into grains (TickerGrain reads it for timestamps)...
            services.AddSingleton<TimeProvider>(TestClock.Instance);
            // ...and used by Orleans' own timer machinery, so advancing the
            // fake clock actually fires grain timers instead of just changing
            // what GetUtcNow returns.
            services.UseTimeProviderForBackgroundAreas(TestClock.Instance);
        });
    }
}

// Starting a cluster costs seconds, so it's shared across a test class via
// IClassFixture rather than being rebuilt per test.
public sealed class ClusterFixture : IAsyncLifetime
{
    public TestCluster Cluster { get; private set; } = null!;

    public IGrainFactory Grains => Cluster.GrainFactory;

    public FakeTimeProvider Clock => TestClock.Instance;

    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        Cluster = builder.Build();
        await Cluster.DeployAsync();
    }

    public async Task DisposeAsync() => await Cluster.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class ClusterCollection : ICollectionFixture<ClusterFixture>
{
    public const string Name = "cluster";
}
