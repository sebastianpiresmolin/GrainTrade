namespace GrainTrade.Abstractions;

// One grain per symbol, keyed by the symbol string. The symbol never changes,
// so it's a stable key.
public interface ITickerGrain : IGrainWithStringKey
{
    Task<TickerQuote> GetQuote();

    // Oldest first. Capped by the grain, so callers can't ask for unbounded history.
    Task<IReadOnlyList<PricePoint>> GetHistory();
}
