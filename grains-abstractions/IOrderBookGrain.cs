namespace GrainTrade.Abstractions;

// One book per symbol, keyed by the symbol string. Owns the resting bids and
// asks and the matching itself — single-threaded execution is what makes
// price-time priority correct without locking the book.
public interface IOrderBookGrain : IGrainWithStringKey
{
    // Records a market-order execution that settled against the ticker price.
    Task Record(Trade trade);

    // Rests a limit order, matching it against the other side first. Returns
    // the fills it generated, newest last.
    Task<IReadOnlyList<Fill>> PlaceLimit(RestingOrder order);

    // Returns false if the order already filled, expired, or never existed.
    Task<bool> Cancel(Guid orderId, Guid accountId);

    // Fills an account hasn't claimed yet. The account settles them itself,
    // so the book never calls back into an account.
    Task<IReadOnlyList<Fill>> ClaimFills(Guid accountId);

    Task<IReadOnlyList<RestingOrder>> GetOpenOrders(Guid accountId);

    Task<BookDepth> GetDepth();

    Task<IReadOnlyList<Trade>> GetRecentTrades();
}
