using GrainTrade.Abstractions;
using GrainTrade.Grains.Models;
using Orleans.Runtime;

namespace GrainTrade.Grains;

public sealed class AccountGrain : Grain, IAccountGrain
{
    private readonly IPersistentState<AccountState> _state;

    // "accounts" is the storage provider the silo host registers.
    public AccountGrain([PersistentState("account", "accounts")] IPersistentState<AccountState> state)
    {
        _state = state;
    }

    public async Task<AccountSummary> Deposit(decimal amount)
    {
        RequirePositive(amount);
        _state.State.CashBalance += amount;
        await _state.WriteStateAsync();
        return ToSummary();
    }

    public async Task<AccountSummary> Withdraw(decimal amount)
    {
        RequirePositive(amount);
        if (amount > _state.State.CashBalance)
        {
            throw new InvalidOperationException(
                $"Insufficient funds: cannot withdraw {amount:C} from a balance of {_state.State.CashBalance:C}.");
        }
        _state.State.CashBalance -= amount;
        await _state.WriteStateAsync();
        return ToSummary();
    }

    public Task<AccountSummary> GetSummary() => Task.FromResult(ToSummary());

    private static void RequirePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
        }
    }

    private AccountSummary ToSummary() => new()
    {
        AccountId = this.GetPrimaryKey(),
        CashBalance = _state.State.CashBalance,
    };
}
