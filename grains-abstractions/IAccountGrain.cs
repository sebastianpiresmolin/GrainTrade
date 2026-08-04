namespace GrainTrade.Abstractions;

// One grain per account, keyed by user Guid. Orleans runs one call at a time per
// grain, so the balance invariant holds without locks.
public interface IAccountGrain : IGrainWithGuidKey
{
    Task<AccountSummary> Deposit(decimal amount);

    // Throws InvalidOperationException on insufficient funds.
    Task<AccountSummary> Withdraw(decimal amount);

    Task<AccountSummary> GetSummary();

    // Fills at the ticker's current price. The account owns the money
    // invariant, so it checks funds/shares itself rather than trusting a
    // caller — throws InvalidOperationException if either is short.
    Task<OrderResult> PlaceOrder(string symbol, OrderSide side, int quantity);

    // Most recent first.
    Task<IReadOnlyList<Trade>> GetTrades();
}
