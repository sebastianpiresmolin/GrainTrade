using GrainTrade.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Orleans.Streams;
using Orleans.TestingHost;

namespace GrainTrade.Tests;

// The silo is configured by type, not by instance, so the clock has to reach it
// through a static. Each fixture assigns a fresh one before deploying, and
// collections run sequentially, so no two clusters share a clock.
internal static class TestClock
{
    public static FakeTimeProvider Current { get; set; } = NewClock();

    public static FakeTimeProvider NewClock() =>
        new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
}

// Mirrors Silo/Program.cs — same provider names the grains ask for.
file sealed class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder silo)
    {
        silo.AddMemoryGrainStorage("accounts");
        silo.AddMemoryGrainStorage("tickers");
        silo.AddMemoryGrainStorage("orderbooks");
        silo.AddMemoryStreams(StreamConstants.Provider);
        silo.AddMemoryGrainStorage("PubSubStore");
        silo.UseInMemoryReminderService();

        var clock = TestClock.Current;
        silo.ConfigureServices(services =>
        {
            // Injected into grains (TickerGrain reads it for timestamps)...
            services.AddSingleton<TimeProvider>(clock);
            // ...and used by Orleans' own timer machinery, so advancing the
            // fake clock actually fires grain timers instead of just changing
            // what GetUtcNow returns.
            services.UseTimeProviderForBackgroundAreas(clock);
        });
    }
}

// The test client subscribes to streams, so it needs the same provider name.
file sealed class TestClientConfigurator : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder client) =>
        client.AddMemoryStreams(StreamConstants.Provider);
}

// Starting a cluster costs seconds, so it's shared across a collection rather
// than being rebuilt per test.
public abstract class ClusterFixture : IAsyncLifetime
{
    public TestCluster Cluster { get; private set; } = null!;

    public IGrainFactory Grains => Cluster.GrainFactory;

    public FakeTimeProvider Clock { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Clock = TestClock.NewClock();
        TestClock.Current = Clock;

        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        builder.AddClientBuilderConfigurator<TestClientConfigurator>();
        Cluster = builder.Build();
        await Cluster.DeployAsync();
    }

    public async Task DisposeAsync() => await Cluster.DisposeAsync();
}

// Separate clusters per collection: the fake clock is cluster-wide state, so
// tests that advance it need to not share one.
public sealed class GrainClusterFixture : ClusterFixture;

public sealed class StreamClusterFixture : ClusterFixture;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ClusterCollection : ICollectionFixture<GrainClusterFixture>
{
    public const string Name = "cluster";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StreamCollection : ICollectionFixture<StreamClusterFixture>
{
    public const string Name = "streams";
}
