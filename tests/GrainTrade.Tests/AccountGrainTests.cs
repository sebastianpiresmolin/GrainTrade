using GrainTrade.Abstractions;

namespace GrainTrade.Tests;

[Collection(ClusterCollection.Name)]
public sealed class AccountGrainTests(ClusterFixture fixture)
{
    // A fresh key per test means a fresh activation with zeroed state — no
    // teardown needed, since grains are addressed rather than created.
    private IAccountGrain NewAccount() => fixture.Grains.GetGrain<IAccountGrain>(Guid.NewGuid());

    [Fact]
    public async Task New_account_starts_empty()
    {
        var summary = await NewAccount().GetSummary();

        Assert.Equal(0m, summary.CashBalance);
    }

    [Fact]
    public async Task Deposit_increases_balance()
    {
        var account = NewAccount();

        await account.Deposit(100m);
        var summary = await account.Deposit(50m);

        Assert.Equal(150m, summary.CashBalance);
    }

    [Fact]
    public async Task Withdraw_decreases_balance()
    {
        var account = NewAccount();
        await account.Deposit(100m);

        var summary = await account.Withdraw(30m);

        Assert.Equal(70m, summary.CashBalance);
    }

    [Fact]
    public async Task Withdraw_beyond_balance_is_rejected()
    {
        var account = NewAccount();
        await account.Deposit(50m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => account.Withdraw(80m));
        Assert.Equal(50m, (await account.GetSummary()).CashBalance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Non_positive_amounts_are_rejected(decimal amount)
    {
        var account = NewAccount();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => account.Deposit(amount));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => account.Withdraw(amount));
    }

    // The reason this project uses Orleans. Read-modify-write on the balance has
    // no lock anywhere; correctness comes from the grain handling one message at
    // a time. Without that guarantee these deposits would interleave and lose
    // updates.
    [Fact]
    public async Task Concurrent_deposits_do_not_lose_updates()
    {
        var account = NewAccount();

        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => account.Deposit(10m)));

        Assert.Equal(1000m, (await account.GetSummary()).CashBalance);
    }

    // Same guarantee on the invariant that matters: only one of these can pass
    // the balance check, so the account cannot be overdrawn.
    [Fact]
    public async Task Concurrent_withdrawals_cannot_overdraw()
    {
        var account = NewAccount();
        await account.Deposit(100m);

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(async _ =>
            {
                try
                {
                    await account.Withdraw(80m);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }));

        Assert.Equal(1, attempts.Count(succeeded => succeeded));
        Assert.Equal(20m, (await account.GetSummary()).CashBalance);
    }
}
