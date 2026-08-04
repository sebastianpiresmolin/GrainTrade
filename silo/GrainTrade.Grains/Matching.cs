using GrainTrade.Abstractions;

namespace GrainTrade.Grains;

// Price-time priority matching. Pure — no Orleans, no clock, no state — so the
// rules are testable on their own.
public static class Matching
{
    // Best counterparty first: for an incoming buy, the cheapest asks; for a
    // sell, the highest bids. Ties go to whoever rested first.
    public static IEnumerable<RestingOrder> Candidates(
        IEnumerable<RestingOrder> book, OrderSide incoming, decimal limitPrice)
    {
        if (incoming == OrderSide.Buy)
        {
            return book
                .Where(o => o.Side == OrderSide.Sell && o.Remaining > 0 && o.LimitPrice <= limitPrice)
                .OrderBy(o => o.LimitPrice)
                .ThenBy(o => o.PlacedAt);
        }

        return book
            .Where(o => o.Side == OrderSide.Buy && o.Remaining > 0 && o.LimitPrice >= limitPrice)
            .OrderByDescending(o => o.LimitPrice)
            .ThenBy(o => o.PlacedAt);
    }

    // The resting order's price wins: it was there first, so it gets the price
    // it asked for. An aggressive order can only improve on its own limit.
    public static decimal ExecutionPrice(RestingOrder resting) => resting.LimitPrice;

    public static IReadOnlyList<DepthLevel> Aggregate(IEnumerable<RestingOrder> book, OrderSide side)
    {
        var levels = book
            .Where(o => o.Side == side && o.Remaining > 0)
            .GroupBy(o => o.LimitPrice)
            .Select(g => new DepthLevel { Price = g.Key, Quantity = g.Sum(o => o.Remaining) });

        return (side == OrderSide.Buy
            ? levels.OrderByDescending(l => l.Price)
            : levels.OrderBy(l => l.Price)).ToArray();
    }
}
