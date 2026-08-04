namespace GrainTrade.Abstractions;

// What a filled order did, plus the account it left behind — one round trip
// instead of an order call followed by a summary call.
[GenerateSerializer]
public sealed record OrderResult
{
    [Id(0)]
    public required Trade Trade { get; init; }

    [Id(1)]
    public required AccountSummary Account { get; init; }
}
