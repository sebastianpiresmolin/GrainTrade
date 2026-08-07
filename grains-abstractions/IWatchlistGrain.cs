namespace GrainTrade.Abstractions;

public interface IWatchlistGrain : IGrainWithGuidKey
{
    Task AddSymbol(string symbol);
    Task RemoveSymbol(string symbol);
    Task<IReadOnlyList<string>> GetSymbols();
}