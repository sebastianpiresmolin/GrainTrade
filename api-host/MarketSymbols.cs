namespace GrainTrade.ApiHost;

// Which symbols exist. No identity, no mutable state, no concurrency needs —
// so it's a plain host constant, not a grain.
public static class MarketSymbols
{
    public static readonly string[] All =
        ["WHEAT", "CORN", "SOY", "OATS", "RICE", "BARLEY"];

    public static bool IsKnown(string symbol) => All.Contains(symbol);
}
