namespace GrainTrade.Abstractions;

// One grain per account, keyed by user Guid. Orleans runs one call at a time per
// grain, so the balance invariant holds without locks.
public interface IAccountGrain : IGrainWithGuidKey
{
    Task<AccountSummary> Deposit(decimal amount);

    // Throws InvalidOperationException on insufficient funds.
    Task<AccountSummary> Withdraw(decimal amount);

    Task<AccountSummary> GetSummary();
}
