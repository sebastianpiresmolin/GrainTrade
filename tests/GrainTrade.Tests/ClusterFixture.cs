using Orleans.TestingHost;

namespace GrainTrade.Tests;

// Configures each in-process test silo. Mirrors Silo/Program.cs — the same
// storage provider name the grain's [PersistentState] attribute asks for.
file sealed class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder silo) => silo.AddMemoryGrainStorage("accounts");
}

// Starting a cluster costs seconds, so it's shared across a test class via
// IClassFixture rather than being rebuilt per test.
public sealed class ClusterFixture : IAsyncLifetime
{
    public TestCluster Cluster { get; private set; } = null!;

    public IGrainFactory Grains => Cluster.GrainFactory;

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
