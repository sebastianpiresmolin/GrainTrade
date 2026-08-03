namespace GrainTrade.Grains;

// Pure price simulation — no Orleans, no clock, so it's testable on its own.
public static class PriceWalk
{
    private const decimal MinPrice = 0.01m;

    // Multiplicative random walk: each step moves the price by up to
    // ±(volatility), so absolute moves scale with price.
    public static decimal Next(decimal price, double volatility, Random random)
    {
        var drift = (decimal)((random.NextDouble() * 2 - 1) * volatility);
        return Math.Max(MinPrice, Math.Round(price * (1 + drift), 2));
    }

    // Deterministic per-symbol starting price, so a symbol looks the same
    // across restarts without hardcoding a table.
    public static decimal SeedPrice(string symbol)
    {
        var hash = symbol.Aggregate(17, (acc, c) => acc * 31 + c);
        return 20m + Math.Abs(hash % 18000) / 100m;
    }
}
