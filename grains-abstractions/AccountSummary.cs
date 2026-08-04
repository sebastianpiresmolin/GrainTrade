namespace GrainTrade.Abstractions;

// DTO returned across the grain boundary — separate from the grain's persisted
// state so the public contract and storage shape can evolve independently.
[GenerateSerializer]
public sealed record AccountSummary
{
    [Id(0)]
    public required Guid AccountId { get; init; }

    [Id(1)]
    public required decimal CashBalance { get; init; }

    [Id(2)]
    public IReadOnlyList<Holding> Holdings { get; init; } = [];

    // Cash committed to resting buy orders. Still on the account, not spendable.
    [Id(3)]
    public decimal ReservedCash { get; init; }

    public decimal AvailableCash => CashBalance - ReservedCash;
}
