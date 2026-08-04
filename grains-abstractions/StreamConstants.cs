namespace GrainTrade.Abstractions;

// Shared by the silo (producer) and the API host (consumer) — both sides must
// name the same provider and namespace or they'd silently never meet.
public static class StreamConstants
{
    public const string Provider = "graintrade";

    // One stream per symbol, keyed by symbol within this namespace.
    public const string TickerNamespace = "ticker-quotes";
}
